using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Shared.Plugins;
using Xunit;
using Assert = Xunit.Assert;
using Moq;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.SemanticKernel.TextGeneration;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MassTransit;

namespace Microsoft.Greenlight.Shared.Tests.Plugins
{
    public class InputOutputTrackingPluginInvocationFilterTests
    {
        private const string STRING_FUNCTION_RESULT = "Test function result";
        private const int INT_FUNCTION_RESULT = 1234;
        

        [Fact]
        public async Task OnFunctionInvocationAsync_WithSimpleStringFunction_AddInvocationDetailsToCollection()
        {
            static string TestFunction() => STRING_FUNCTION_RESULT;

            var pluginSourceReferenceItem = await BaselineFunctionExecutionTest(TestFunction);

            Assert.Equal(STRING_FUNCTION_RESULT, pluginSourceReferenceItem.SourceOutput);
        }

        [Fact]
        public async Task OnFunctionInvocationAsync_WithContentStatePlugin_ShouldNotAddReferenceItem()
        {            
            static int TestFunction() => INT_FUNCTION_RESULT;

            // Relies on Asserts in BaselineFunctionExecutionTest to verify that the
            // reference item is not added. As o reference item should be added
            // no additional validation is required.
            await BaselineFunctionExecutionTest(TestFunction, "ContentState", false);
        }

        [Fact]
        public async Task OnFunctionInvocationAsync_WithSimpleIntFunction_AddInvocationDetailsToCollection()
        {
            static int TestFunction() => INT_FUNCTION_RESULT;

            var pluginSourceReferenceItem = await BaselineFunctionExecutionTest(TestFunction);

            Assert.Equal(INT_FUNCTION_RESULT, JsonSerializer.Deserialize<int>(pluginSourceReferenceItem.SourceOutput!));
        }

        [Fact]
        public async Task OnFunctionInvocationAsync_WithStringParameter_AddInvocationDetailsToCollection()
        {
            static string TestFunction(string stringArg) => STRING_FUNCTION_RESULT;

            var pluginSourceReferenceItem = await BaselineFunctionExecutionTest(TestFunction);

            var sourceInput = JsonSerializer.Deserialize<JsonElement>(pluginSourceReferenceItem.SourceInputJson!);
            Assert.Equal(TestTextGeneration.STRING_ARG_VALUE, sourceInput.GetProperty("stringArg").GetString());
        }

        [Fact]
        public async Task OnFunctionInvocationAsync_WithIntParameter_AddInvocationDetailsToCollection()
        {
            static string TestFunction(int intArg) => STRING_FUNCTION_RESULT;

            var pluginSourceReferenceItem = await BaselineFunctionExecutionTest(TestFunction);

            Assert.Equal(STRING_FUNCTION_RESULT, pluginSourceReferenceItem.SourceOutput);
            var sourceInput = JsonSerializer.Deserialize<JsonElement>(pluginSourceReferenceItem.SourceInputJson!);
            // Deserializing as a JsonElement causes the int value to be a string,
            // more concerned about excercising the non-string path then how we deserialize the result.
            Assert.Equal(TestTextGeneration.INT_ARG_VALUE.ToString(), sourceInput.GetProperty("intArg").GetString());
        }

        private static async Task<PluginSourceReferenceItem> BaselineFunctionExecutionTest(
            Delegate function,
            string pluginName = "TestPlugin",
            bool shouldAddReference = true)
        {
            // Arrange
            var builder = Kernel.CreateBuilder();
            var collector = new Mock<IPluginSourceReferenceCollector>();
            var executionId = Guid.NewGuid();

            builder.Services.AddSingleton(collector.Object);
            builder.Services.AddSingleton<ITextGenerationService>(new TestTextGeneration());
            builder.Services.AddSingleton<IFunctionInvocationFilter, InputOutputTrackingPluginInvocationFilter>();

            var kernel = builder.Build();

            kernel.Data.Add("System-ExecutionId", executionId.ToString());
            
            var kernalFunction = KernelFunctionFactory.CreateFromMethod(function);
            kernel.ImportPluginFromFunctions(pluginName, [kernalFunction]);

            var executionSettings = new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Required([kernalFunction], autoInvoke: true)
            };

            // Act
            await kernel.InvokePromptAsync("My prompt", new(executionSettings));

            // Assert
            if (!shouldAddReference)
            {
                collector.Verify(c => c.Add(It.IsAny<Guid>(), It.IsAny<PluginSourceReferenceItem>()), Times.Never);
                return null!;
            }
            collector.Verify(c => c.Add(It.IsAny<Guid>(), It.IsAny<PluginSourceReferenceItem>()), Times.Once);
            var callArguments = collector.Invocations[0].Arguments;
            Assert.Equal(callArguments[0], executionId);
            var referenceItem = (PluginSourceReferenceItem)callArguments[1];
            Assert.StartsWith(pluginName, referenceItem.PluginIdentifier);
            Assert.EndsWith(kernalFunction.Name, referenceItem.PluginIdentifier);

            return referenceItem;
        }


        // Test implmentation of ITextGenerationService to enable testing of OnFunctionInvocationAsync
        // without requiring an instance of OpenAI. 
        // Calling the function directly isn't possible as it requires an instance of FunctionInvocationContext,
        // which has a private constructor.
        private class TestTextGeneration : ITextGenerationService
        {
            public const string STRING_ARG_VALUE = "Test String";
            public const int INT_ARG_VALUE = 5678;

            public IReadOnlyDictionary<string, object?> Attributes => throw new NotImplementedException();

            public async IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(
                string prompt,
                PromptExecutionSettings? executionSettings = null,
                Kernel? kernel = null,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                // Test implementation that yields 3 test streaming text contents
                yield return await Task.FromResult(new StreamingTextContent("Test 1"));
                yield return await Task.FromResult(new StreamingTextContent("Test 2"));
                yield return await Task.FromResult(new StreamingTextContent("Test 3"));
            }

            public async Task<IReadOnlyList<TextContent>> GetTextContentsAsync(
                string prompt,
                PromptExecutionSettings? executionSettings = null,
                Kernel? kernel = null,
                CancellationToken cancellationToken = default)
            {
                // For each function defined in the kernel, we invoke the function.
                var results = new List<TextContent>();

                foreach(var plugin in kernel!.Plugins)
                {
                    foreach (var functionMetaData in plugin.GetFunctionsMetadata())
                    {
                        plugin.TryGetFunction(functionMetaData.Name, out var function);
                        FunctionResult result;
                        if(functionMetaData.Parameters.Count == 0)
                        {
                            result = await kernel.InvokeAsync(function!, null, cancellationToken);
                        }
                        else
                        {
                            var kernelArguments = new KernelArguments();
                            foreach (var parameter in functionMetaData.Parameters)
                            {
                                switch (parameter.ParameterType!.Name)
                                {
                                    case "String":
                                        kernelArguments.Add(parameter.Name, STRING_ARG_VALUE);
                                        break;
                                    case "Int32":
                                        kernelArguments.Add(parameter.Name, INT_ARG_VALUE);
                                        break;
                                    default:
                                        kernelArguments.Add(parameter.Name, parameter.DefaultValue);
                                        break;
                                }
                            }
                            result = await kernel.InvokeAsync(function!, kernelArguments, cancellationToken);
                        }
                        results.Add(new TextContent { Text = result.ToString() });
                    }
                }
                results.Add(new TextContent { Text = "Test Content" });

                return results;
            }
        }
    }
}
