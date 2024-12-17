using System.Linq.Expressions;
using System.Text.Json;
using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.KernelMemory;

namespace Microsoft.Greenlight.Shared.Mappings;

public class GeneratedDocumentProfile : Profile
{
    public GeneratedDocumentProfile()
    {
        // Mapping for ContentNode


        CreateMap<ContentNode, ContentNodeInfo>()
            .ForMember(dest => dest.Children, opt => opt.Ignore())
            .ForMember(dest => dest.ContentNodeSystemItem, opt => opt.Ignore())
            .ForMember(dest => dest.ContentNodeSystemItemId, opt => opt.MapFrom(src => src.ContentNodeSystemItemId))
            .AfterMap((src, dest, context) =>
            {
                // Map children recursively
                if (src.Children != null && src.Children.Any())
                {
                    dest.Children = src.Children.Select(child => context.Mapper.Map<ContentNodeInfo>(child)).ToList();
                }
            });

        CreateMap<ContentNodeInfo, ContentNode>()
            .ForMember(dest => dest.Children, opt => opt.Ignore())
            .ForMember(dest => dest.ContentNodeSystemItem, opt => opt.Ignore())
            .ForMember(dest => dest.ContentNodeSystemItemId, opt => opt.MapFrom(src => src.ContentNodeSystemItemId))
            .AfterMap((src, dest, context) =>
            {
                if (src.Children != null && src.Children.Any())
                {
                    dest.Children = src.Children.Select(child => context.Mapper.Map<ContentNode>(child)).ToList();
                }
            });

        // Mapping for ContentNodeSystemItem
        CreateMap<ContentNodeSystemItem, ContentNodeSystemItemInfo>()
            .ReverseMap();

        // Polymorphic mapping for SourceReferenceItem
        CreateMap<SourceReferenceItem, SourceReferenceItemInfo>()
            .ReverseMap();

        CreateMap<PluginSourceReferenceItem, PluginSourceReferenceItemInfo>()
            .IncludeBase<SourceReferenceItem, SourceReferenceItemInfo>();

        // Intermediate mapping for KernelMemoryDocumentSourceReferenceItem which is the base for other derived types
        CreateMap<KernelMemoryDocumentSourceReferenceItem, KernelMemoryDocumentSourceReferenceItemInfo>()
            .ForMember(dest => dest.Citations, opt => opt.MapFrom(src => DeserializeCitations(src.CitationJsons)))
            .IncludeBase<SourceReferenceItem, SourceReferenceItemInfo>();

        CreateMap<DocumentLibrarySourceReferenceItem, DocumentLibrarySourceReferenceItemInfo>()
            .IncludeBase<KernelMemoryDocumentSourceReferenceItem, KernelMemoryDocumentSourceReferenceItemInfo>();

        CreateMap<DocumentProcessRepositorySourceReferenceItem, DocumentProcessRepositorySourceReferenceItemInfo>()
            .IncludeBase<KernelMemoryDocumentSourceReferenceItem, KernelMemoryDocumentSourceReferenceItemInfo>();

        // Mapping for GeneratedDocument
        CreateMap<GeneratedDocument, GeneratedDocumentInfo>()
            .ForMember(dest => dest.ContentNodes, opt => opt.Ignore())
            .AfterMap((src, dest, context) =>
            {
                if (src.ContentNodes != null && src.ContentNodes.Any())
                {
                    dest.ContentNodes = src.ContentNodes.Select(cn => context.Mapper.Map<ContentNodeInfo>(cn)).ToList();
                }
            });

        CreateMap<GeneratedDocumentInfo, GeneratedDocument>()
            .ForMember(dest => dest.ContentNodes, opt => opt.Ignore())
            .AfterMap((src, dest, context) =>
            {
                if (src.ContentNodes != null && src.ContentNodes.Any())
                {
                    dest.ContentNodes = src.ContentNodes.Select(cn => context.Mapper.Map<ContentNode>(cn)).ToList();
                }
            });
    }

    private static List<Citation> DeserializeCitations(List<string> citationJsons)
    {
        var citations = new List<Citation>();
        foreach (var citationJson in citationJsons)
        {
            var citation = JsonSerializer.Deserialize<Citation>(citationJson);
            if (citation != null)
            {
                citations.Add(citation);
            }
        }
        return citations;
    }
}