namespace RockDMZ.Infrastructure
{
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using RockDMZ.Domain;

    public class EntityModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            return typeof(IEntity).IsAssignableFrom(context.Metadata.ModelType) ? new EntityModelBinder() : null;
        }
    }
}