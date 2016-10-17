namespace AgileObjects.AgileMapper.ObjectPopulation
{
    using System.Linq.Expressions;
    using Members;

    internal interface IMemberPopulation
    {
        IMemberMapperData MapperData { get; }

        bool IsSuccessful { get; }

        Expression SourceMemberTypeTest { get; }

        Expression GetPopulation();
    }
}