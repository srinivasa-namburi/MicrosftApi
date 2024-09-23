using AutoMapper;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models.DocumentProcess;

namespace ProjectVico.V2.Shared.Mappings;

public class DocumentOutlineInfoProfile : Profile
{
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

    private void OrderChildren(DocumentOutlineInfo item)
    {
        if (item.OutlineItems != null && item.OutlineItems.Any())
        {
            item.OutlineItems = item.OutlineItems.OrderBy(c => c.OrderIndex).ToList();
            foreach (var child in item.OutlineItems)
            {
                OrderChildren(child);
            }
        }
    }

    private void OrderChildren(DocumentOutline item)
    {
        if (item.OutlineItems != null && item.OutlineItems.Any())
        {
            item.OutlineItems = item.OutlineItems.OrderBy(c => c.OrderIndex).ToList();
            foreach (var child in item.OutlineItems)
            {
                OrderChildren(child);
            }
        }
    }

    private void OrderChildren(DocumentOutlineItemInfo item)
    {
        if (item.Children != null && item.Children.Any())
        {
            item.Children = item.Children.OrderBy(c => c.OrderIndex).ToList();
            foreach (var child in item.Children)
            {
                OrderChildren(child);
            }
        }
    }

    private void OrderChildren(DocumentOutlineItem item)
    {
        if (item.Children != null && item.Children.Any())
        {
            item.Children = item.Children.OrderBy(c => c.OrderIndex).ToList();
            foreach (var child in item.Children)
            {
                OrderChildren(child);
            }
        }
    }
}