using AutoMapper;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Mappings;

/// <summary>
/// Profile for mapping between <see cref="DocumentOutline"/> and <see cref="DocumentOutlineInfo"/>, and between DocumentOutlineItem and DocumentOutlineItemInfo.
/// </summary>
public class DocumentOutlineInfoProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentOutlineInfoProfile"/> class.
    /// Defines the mapping between <see cref="DocumentOutline"/> and <see cref="DocumentOutlineInfo"/>, 
    /// and between <see cref="DocumentOutlineItem"/> and <see cref="DocumentOutlineItemInfo"/>.
    /// </summary>
    public DocumentOutlineInfoProfile()
    {
        CreateMap<DocumentOutline, DocumentOutlineInfo>()
            .AfterMap((src, dest) =>
            {
                OrderChildren(dest);
            });

        CreateMap<DocumentOutlineInfo, DocumentOutline>()
            .AfterMap((src, dest) =>
            {
                OrderChildren(dest);
            });


        CreateMap<DocumentOutlineItem, DocumentOutlineItemInfo>()
            .AfterMap((src, dest) =>
            {
                OrderChildren(dest);
            });

        CreateMap<DocumentOutlineItemInfo, DocumentOutlineItem>()
            .AfterMap((src, dest) =>
            {
                OrderChildren(dest);
            });
    }

    private static void OrderChildren(DocumentOutlineInfo item)
    {
        if (item.OutlineItems != null && item.OutlineItems.Count != 0)
        {
            item.OutlineItems = item.OutlineItems.OrderBy(c => c.OrderIndex).ToList();
            foreach (var child in item.OutlineItems)
            {
                OrderChildren(child);
            }
        }
    }

    private static void OrderChildren(DocumentOutline item)
    {
        if (item.OutlineItems != null && item.OutlineItems.Count != 0)
        {
            item.OutlineItems = item.OutlineItems.OrderBy(c => c.OrderIndex).ToList();
            foreach (var child in item.OutlineItems)
            {
                OrderChildren(child);
            }
        }
    }

    private static void OrderChildren(DocumentOutlineItemInfo item)
    {
        if (item.Children != null && item.Children.Count != 0)
        {
            item.Children = item.Children.OrderBy(c => c.OrderIndex).ToList();
            foreach (var child in item.Children)
            {
                OrderChildren(child);
            }
        }
    }

    private static void OrderChildren(DocumentOutlineItem item)
    {
        if (item.Children != null && item.Children.Count != 0)
        {
            item.Children = item.Children.OrderBy(c => c.OrderIndex).ToList();
            foreach (var child in item.Children)
            {
                OrderChildren(child);
            }
        }
    }
}
