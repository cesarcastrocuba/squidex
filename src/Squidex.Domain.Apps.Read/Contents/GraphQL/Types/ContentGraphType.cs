﻿// ==========================================================================
//  SchemaGraphType.cs
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex Group
//  All rights reserved.
// ==========================================================================

using System;
using GraphQL.Resolvers;
using GraphQL.Types;
using Squidex.Infrastructure;
using Schema = Squidex.Domain.Apps.Core.Schemas.Schema;

namespace Squidex.Domain.Apps.Read.Contents.GraphQL.Types
{
    public sealed class ContentGraphType : ObjectGraphType<IContentEntity>
    {
        private readonly Schema schema;
        private readonly IGraphQLContext context;

        public ContentGraphType(Schema schema, IGraphQLContext context)
        {
            this.schema = schema;
            this.context = context;

            Name = $"{schema.Name.ToPascalCase()}Dto";
        }

        public void Initialize()
        {
            var schemaName = schema.Properties.Label.WithFallback(schema.Name);

            AddField(new FieldType
            {
                Name = "id",
                Resolver = Resolver(x => x.Id.ToString()),
                ResolvedType = new NonNullGraphType(new StringGraphType()),
                Description = $"The id of the {schemaName} content."
            });

            AddField(new FieldType
            {
                Name = "version",
                Resolver = Resolver(x => x.Version),
                ResolvedType = new NonNullGraphType(new IntGraphType()),
                Description = $"The version of the {schemaName} content."
            });

            AddField(new FieldType
            {
                Name = "created",
                Resolver = Resolver(x => x.Created.ToDateTimeUtc()),
                ResolvedType = new NonNullGraphType(new DateGraphType()),
                Description = $"The date and time when the {schemaName} content has been created."
            });

            AddField(new FieldType
            {
                Name = "createdBy",
                Resolver = Resolver(x => x.CreatedBy.ToString()),
                ResolvedType = new NonNullGraphType(new StringGraphType()),
                Description = $"The user that has created the {schemaName} content."
            });

            AddField(new FieldType
            {
                Name = "lastModified",
                Resolver = Resolver(x => x.LastModified.ToDateTimeUtc()),
                ResolvedType = new NonNullGraphType(new DateGraphType()),
                Description = $"The date and time when the {schemaName} content has been modified last."
            });

            AddField(new FieldType
            {
                Name = "lastModifiedBy",
                Resolver = Resolver(x => x.LastModifiedBy.ToString()),
                ResolvedType = new NonNullGraphType(new StringGraphType()),
                Description = $"The user that has updated the {schemaName} content last."
            });

            AddField(new FieldType
            {
                Name = "data",
                Resolver = Resolver(x => x.Data),
                ResolvedType = new NonNullGraphType(new ContentDataGraphType(schema, context)),
                Description = $"The data of the {schemaName} content."
            });

            Description = $"The structure of a {schemaName} content type.";
        }

        private static IFieldResolver Resolver(Func<IContentEntity, object> action)
        {
            return new FuncFieldResolver<IContentEntity, object>(c => action(c.Source));
        }
    }
}
