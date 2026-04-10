using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FoodyBackend;

public sealed class RequiredQueryParameterOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is null || operation.Parameters.Count == 0)
        {
            return;
        }

        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
        {
            var apiParameter = context.ApiDescription.ParameterDescriptions.FirstOrDefault(
                description =>
                    string.Equals(description.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                    description.Source?.CanAcceptDataFrom(BindingSource.Query) == true &&
                    description.IsRequired);

            if (apiParameter is not null)
            {
                parameter.Required = true;
            }
        }
    }
}
