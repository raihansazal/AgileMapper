namespace AgileObjects.AgileMapper.ObjectPopulation
{
    using System.Globalization;

    internal class ObjectMapperFactory
    {
        public IObjectMapper<TTarget> CreateFor<TSource, TTarget>(IObjectMappingContext omc)
        {
            var mapperKey = string.Format(
                CultureInfo.InvariantCulture,
                "{0} -> {1}: {2} ObjectMapper",
                omc.SourceMember.Signature,
                omc.TargetMember.Signature,
                omc.RuleSetName);

            var mapper = omc.MapperContext.Cache.GetOrAdd(mapperKey, k =>
            {
                var lambda = omc.TargetMember.IsEnumerable
                    ? EnumerableMappingLambdaFactory<TSource, TTarget>.Instance.Create(omc)
                    : ComplexTypeMappingLambdaFactory<TSource, TTarget>.Instance.Create(omc);

                return new ObjectMapper<TSource, TTarget>(lambda);
            });

            return mapper;
        }
    }
}