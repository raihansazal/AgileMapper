namespace AgileObjects.AgileMapper.DataSources
{
    using Members;

    internal class NullDataSourceFactory : IDataSourceFactory
    {
        public static readonly IDataSourceFactory Instance = new NullDataSourceFactory();

        public IDataSource Create(IMemberMapperData mapperData) => NullDataSource.Default;
    }
}