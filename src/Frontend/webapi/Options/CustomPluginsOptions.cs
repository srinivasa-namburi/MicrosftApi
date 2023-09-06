// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace ProjectVico.Frontend.API.Options;

public class CustomPluginsOptions
{
    public const string PropertyName = "CustomPlugins";
    public List<string>? OpenAPIPlugins { get; set; }
}
