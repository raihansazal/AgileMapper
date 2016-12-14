namespace AgileObjects.AgileMapper.DataSources
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using Extensions;
    using Members;

    internal class DataSourceSet : IEnumerable<IDataSource>
    {
        private readonly IList<IDataSource> _dataSources;
        private readonly List<ParameterExpression> _variables;

        public DataSourceSet(params IDataSource[] dataSources)
        {
            _dataSources = dataSources;
            _variables = new List<ParameterExpression>();
            None = dataSources.Length == 0;

            if (None)
            {
                return;
            }

            foreach (var dataSource in dataSources)
            {
                HasValue = HasValue || dataSource.IsValid;
                _variables.AddRange(dataSource.Variables);

                if (dataSource.SourceMemberTypeTest != null)
                {
                    SourceMemberTypeTest = dataSource.SourceMemberTypeTest;
                }
            }
        }

        public bool None { get; }

        public bool HasValue { get; }

        public Expression SourceMemberTypeTest { get; }

        public IEnumerable<ParameterExpression> Variables => _variables;

        public IDataSource this[int index] => _dataSources[index];

        public Expression GetValueExpression() => _dataSources.ReverseChain();

        public Expression GetPopulationExpression(IMemberMapperData mapperData)
        {
            var fallbackValue = GetFallbackValueOrNull(mapperData);
            var excludeFallback = fallbackValue == null;

            Expression population = null;

            for (var i = _dataSources.Count - 1; i >= 0; --i)
            {
                var dataSource = _dataSources[i];

                if (i == _dataSources.Count - 1)
                {
                    if (excludeFallback)
                    {
                        continue;
                    }

                    population = mapperData.GetTargetMemberPopulation(fallbackValue);

                    if (dataSource.IsConditional)
                    {
                        population = dataSource.AddCondition(population);
                    }

                    continue;
                }

                if (population == null)
                {
                    population = dataSource.AddCondition(dataSource.GetMemberPopulation(mapperData));
                    continue;
                }

                population = Expression.IfThenElse(
                    dataSource.Condition,
                    dataSource.GetMemberPopulation(mapperData),
                    population);
            }

            return population;
        }

        private Expression GetFallbackValueOrNull(IMemberMapperData mapperData)
        {
            var fallbackDataSource = _dataSources.Last().Value;

            if (fallbackDataSource.NodeType == ExpressionType.Coalesce)
            {
                return ((BinaryExpression)fallbackDataSource).Right;
            }

            var targetMemberAccess = mapperData.GetTargetMemberAccess();

            if (fallbackDataSource.ToString() == targetMemberAccess.ToString())
            {
                return null;
            }

            return fallbackDataSource;
        }

        #region IEnumerable<IDataSource> Members

        public IEnumerator<IDataSource> GetEnumerator() => _dataSources.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}