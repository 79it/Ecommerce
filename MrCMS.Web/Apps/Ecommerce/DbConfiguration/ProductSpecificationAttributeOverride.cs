﻿using FluentNHibernate.Automapping;
using FluentNHibernate.Automapping.Alterations;
using MrCMS.Web.Apps.Ecommerce.Entities.Products;

namespace MrCMS.Web.Apps.Ecommerce.DbConfiguration
{
    public class ProductSpecificationAttributeOverride : IAutoMappingOverride<ProductSpecificationAttribute>
    {
        public void Override(AutoMapping<ProductSpecificationAttribute> mapping)
        {
            mapping.Map(x => x.Name).Index("IX_ProductSpecificationAttribute_Name");
        }
    }
}