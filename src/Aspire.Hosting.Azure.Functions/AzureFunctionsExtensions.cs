// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable AZPROVISION001

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Functions;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding the Azure Functions resources to the application model.
/// </summary>
public static class AzureFunctionsExtensions
{
    /// <summary>
    /// Configures the resource to be published as Azure Functions when deployed via Azure Developer CLI.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{FunctionsResource}"/> builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FunctionsResource}"/> builder.</returns>
    public static IResourceBuilder<FunctionsResource> PublishAsAzureFunctions(this IResourceBuilder<FunctionsResource> builder)
    {
        return builder.PublishAsAzureFunctions(null);
    }

    /// <summary>
    /// Configures the resource to be published as Azure Functions when deployed via Azure Developer CLI.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{FunctionsResource}"/> builder.</param>
    /// <param name="configureResource">Callback to configure the underlying <see cref="global::Azure.Provisioning.Functions"/> resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FunctionsResource}"/> builder.</returns>
    [Experimental("AZPROVISION001", UrlFormat = "https://aka.ms/dotnet/aspire/diagnostics#{0}")]
    public static IResourceBuilder<FunctionsResource> PublishAsAzureFunctions(this IResourceBuilder<FunctionsResource> builder, Action<IResourceBuilder<AzureFunctionsResource>, ResourceModuleConstruct, Functions>? configureResource)
    {
        return builder.PublishAsAzureFunctionsInternal(configureResource);
    }

    internal static IResourceBuilder<FunctionsResource> PublishAsAzureFunctionsInternal(this IResourceBuilder<FunctionsResource> builder, Action<IResourceBuilder<AzureFunctionsResource>, ResourceModuleConstruct, Functions>? configureResource, bool useProvisioner = false)
    {
        builder.ApplicationBuilder.AddAzureProvisioning();

        var configureConstruct = (ResourceModuleConstruct construct) =>
        {
            var function = new Function(construct, name: builder.Resource.Name);

            function.Properties.Tags["aspire-resource-name"] = construct.Resource.Name;

            var vaultNameParameter = new Parameter("keyVaultName");
            construct.AddParameter(vaultNameParameter);

            var  keyVault = KeyVault.FromExisting(construct, "keyVaultName");

            var keyVaultSecret = new KeyVaultSecret(construct, keyVault, "connectionString");
            keyVaultSecret.AssignProperty(
                x => x.Properties.Value,
                $$"""'${{{function.Name}}.properties.hostName},ssl=true,password=${{{function.Name}}.listKeys({{function.Name}}.apiVersion).primaryKey}'"""
                );

            var resource = (AzureFunctionsResource)construct.Resource;
            var resourceBuilder = builder.ApplicationBuilder.CreateResourceBuilder(resource);
            configureResource?.Invoke(resourceBuilder, construct, function);
        };

        var resource = new AzureFunctionsResource(builder.Resource, configureConstruct);
        var resourceBuilder = builder.ApplicationBuilder.CreateResourceBuilder(resource)
                                     .WithParameter(AzureBicepResource.KnownParameters.KeyVaultName)
                                     .WithManifestPublishingCallback(resource.WriteToManifest);

        if (useProvisioner)
        {
            // Used to hold a reference to the azure surrogate for use with the provisioner.
            builder.WithAnnotation(new AzureBicepResourceAnnotation(resource));
            builder.WithConnectionStringRedirection(resource);

            // Remove the container annotation so that DCP doesn't do anything with it.
            if (builder.Resource.Annotations.OfType<ContainerImageAnnotation>().SingleOrDefault() is { } containerAnnotation)
            {
                builder.Resource.Annotations.Remove(containerAnnotation);
            }
        }

        return builder;
    }

    /// <summary>
    /// Configures resource to use Azure for local development and when doing a deployment via the Azure Developer CLI.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{FunctionsResource}"/> builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FunctionsResource}"/> builder.</returns>
    public static IResourceBuilder<FunctionsResource> AsAzureFunctions(this IResourceBuilder<FunctionsResource> builder)
    {
        return builder.AsAzureFunctions(null);
    }

    /// <summary>
    /// Configures resource to use Azure for local development and when doing a deployment via the Azure Developer CLI.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{FunctionsResource}"/> builder.</param>
    /// <param name="configureResource">Callback to configure the underlying <see cref="global::Azure.Provisioning.Functions"/> resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FunctionsResource}"/> builder.</returns>
    [Experimental("AZPROVISION001", UrlFormat = "https://aka.ms/dotnet/aspire/diagnostics#{0}")]
    public static IResourceBuilder<FunctionsResource> AsAzureFunctions(this IResourceBuilder<FunctionsResource> builder, Action<IResourceBuilder<AzureFunctionsResource>, ResourceModuleConstruct, Functions>? configureResource)
    {
        return builder.PublishAsAzureFunctionsInternal(configureResource, useProvisioner: true);
    }
}
