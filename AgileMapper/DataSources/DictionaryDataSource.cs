﻿namespace AgileObjects.AgileMapper.DataSources
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Extensions;
    using Members;
    using ObjectPopulation;

    internal class DictionaryDataSourceFactory : IConditionalDataSourceFactory
    {
        public bool IsFor(IMemberMappingContext context)
        {
            return context.SourceType.IsGenericType &&
                  (context.SourceType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) &&
                  DictionaryHasUseableTypes(context);
        }

        private static bool DictionaryHasUseableTypes(IMemberMappingContext context)
        {
            var keyAndValueTypes = context.SourceType.GetGenericArguments();

            if (keyAndValueTypes[0] != typeof(string))
            {
                return false;
            }

            if (context.TargetMember.IsEnumerable)
            {
                return (keyAndValueTypes[1] == typeof(object)) ||
                       (keyAndValueTypes[1] == context.TargetMember.LeafMember.ElementType) ||
                        keyAndValueTypes[1].IsEnumerable();
            }

            return context
                .MapperContext
                .ValueConverters
                .CanConvert(keyAndValueTypes[1], context.TargetMember.Type);
        }

        public IDataSource Create(IMemberMappingContext context)
            => new DictionaryDataSource(context);

        private class DictionaryDataSource : DataSourceBase
        {
            #region Cached Items

            private static readonly MethodInfo _linqIntersectMethod = typeof(Enumerable)
                .GetMethods(Constants.PublicStatic)
                .First(m => m.Name == "Intersect" && m.GetParameters().Length == 3)
                .MakeGenericMethod(typeof(string));

            private static readonly MethodInfo _linqFirstOrDefaultMethod = typeof(Enumerable)
                .GetMethods(Constants.PublicStatic)
                .First(m => m.Name == "FirstOrDefault" && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(string));

            #endregion

            public DictionaryDataSource(IMemberMappingContext context)
                : this(
                      context,
                      Expression.Variable(
                          context.SourceType.GetGenericArguments().Last(),
                          context.TargetMember.Name.ToCamelCase()))
            {
            }

            private DictionaryDataSource(IMemberMappingContext context, ParameterExpression variable)
                : base(context.SourceMember, Enumerable.Empty<Expression>(), new[] { variable }, GetValueParsing(variable, context))
            {
            }

            private static Expression GetValueParsing(Expression variable, IMemberMappingContext context)
            {
                var potentialNames = GetPotentialNames(context);

                var tryGetValueCall = GetTryGetValueCall(
                    variable,
                    potentialNames.Select(Expression.Constant),
                    context);

                var dictionaryValueOrFallback = Expression.Condition(
                    tryGetValueCall,
                    GetValue(variable, context),
                    GetFallbackValue(variable, potentialNames, context));

                return dictionaryValueOrFallback;
            }

            private static string[] GetPotentialNames(IMemberMappingContext context)
            {
                var alternateNames = context
                    .TargetMember
                    .MemberChain
                    .Skip(1)
                    .Select(context.MapperContext.NamingSettings.GetAlternateNamesFor)
                    .CartesianProduct();

                var flattenedNameSet = (context.TargetMember.MemberChain.Count() == 2)
                    ? alternateNames.SelectMany(names => names)
                    : alternateNames.ToArray().SelectMany(context.MapperContext.NamingSettings.GetJoinedNamesFor);

                return flattenedNameSet.ToArray();
            }

            private static Expression GetTryGetValueCall(
                Expression variable,
                IEnumerable<Expression> potentialNames,
                IMemberMappingContext context)
            {
                var linqIntersect = Expression.Call(
                    _linqIntersectMethod,
                    Expression.Property(context.SourceObject, "Keys"),
                    Expression.NewArrayInit(typeof(string), potentialNames),
                    CaseInsensitiveStringComparer.InstanceMember);

                var intersectionFirstOrDefault = Expression.Call(_linqFirstOrDefaultMethod, linqIntersect);
                var emptyString = Expression.Field(null, typeof(string), "Empty");
                var matchingNameOrEmptyString = Expression.Coalesce(intersectionFirstOrDefault, emptyString);

                var tryGetValueCall = Expression.Call(
                    context.SourceObject,
                    context.SourceObject.Type.GetMethod("TryGetValue", Constants.PublicInstance),
                    matchingNameOrEmptyString,
                    variable);

                return tryGetValueCall;
            }

            private static Expression GetValue(Expression variable, IMemberMappingContext context)
            {
                if (context.TargetMember.IsSimple)
                {
                    return context
                        .MapperContext
                        .ValueConverters
                        .GetConversion(variable, context.TargetMember.Type);
                }

                return context.GetMapCall(variable, 0);
            }

            private static Expression GetFallbackValue(
                Expression variable,
                IEnumerable<string> potentialNames,
                IMemberMappingContext context)
            {
                var fallbackValue = context
                    .MappingContext
                    .RuleSet
                    .FallbackDataSourceFactory
                    .Create(context)
                    .Value;

                if (context.TargetMember.IsSimple)
                {
                    return fallbackValue;
                }

                var enumerablePopulation = GetEnumerablePopulation(variable, potentialNames, context);
                var enumerablePopulationOrFallback = Expression.Coalesce(enumerablePopulation, fallbackValue);

                return enumerablePopulationOrFallback;
            }

            private static Expression GetEnumerablePopulation(
                Expression variable,
                IEnumerable<string> potentialNames,
                IMemberMappingContext context)
            {
                var sourceElementType = context.SourceType.GetGenericArguments()[1];
                var sourceList = Expression.Variable(typeof(List<>).MakeGenericType(sourceElementType), "sourceList");
                var counter = Expression.Variable(typeof(int), "i");

                var potentialNameConstants = potentialNames.Select(name =>
                {
                    var nameAndOpenBrace = Expression.Constant(name + "[");
                    var counterString = context.MapperContext.ValueConverters.GetConversion(counter, typeof(string));
                    var closeBrace = Expression.Constant("]");

                    var stringConcatMethod = typeof(string)
                        .GetMethods(Constants.PublicStatic)
                        .First(m => (m.Name == "Concat") &&
                            (m.GetParameters().Length == 3) &&
                            (m.GetParameters().First().ParameterType == typeof(string)));

                    var nameConstant = Expression.Call(
                        null,
                        stringConcatMethod,
                        nameAndOpenBrace,
                        counterString,
                        closeBrace);

                    return nameConstant;
                });

                var tryGetValueCall = GetTryGetValueCall(variable, potentialNameConstants, context);
                var loopBreak = Expression.Break(Expression.Label());
                var ifNotTryGetValueBreak = Expression.IfThen(Expression.Not(tryGetValueCall), loopBreak);

                var sourceListAddCall = Expression.Call(sourceList, "Add", Constants.NoTypeArguments, variable);
                var incrementCounter = Expression.PreIncrementAssign(counter);

                var loopBody = Expression.Block(
                    ifNotTryGetValueBreak,
                    sourceListAddCall,
                    incrementCounter);

                var populationLoop = Expression.Loop(loopBody, loopBreak.Target);

                var mapCall = context.GetMapCall(sourceList, 0);

                var enumerablePopulation = Expression.Block(
                    new[] { sourceList, counter },
                    Expression.Assign(sourceList, EnumerableTypes.GetEnumerableEmptyInstance(sourceList.Type)),
                    Expression.Assign(counter, Expression.Constant(0)),
                    populationLoop,
                    mapCall);

                return enumerablePopulation;
            }
        }
    }
}