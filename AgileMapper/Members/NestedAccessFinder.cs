namespace AgileObjects.AgileMapper.Members
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using Extensions;
    using ReadableExpressions.Extensions;

    internal class NestedAccessFinder : ExpressionVisitor
    {
        private static readonly object _syncLock = new object();

        private readonly Expression _mappingDataObject;
        private readonly ICollection<Expression> _stringMemberAccessSubjects;
        private readonly ICollection<string> _nullCheckSubjects;
        private readonly Dictionary<string, Expression> _memberAccessesByPath;
        private bool _includeTargetNullChecking;

        public NestedAccessFinder(Expression mappingDataObject)
        {
            _mappingDataObject = mappingDataObject;
            _stringMemberAccessSubjects = new List<Expression>();
            _nullCheckSubjects = new List<string>();
            _memberAccessesByPath = new Dictionary<string, Expression>();
        }

        public Expression[] FindIn(Expression expression, bool targetCanBeNull)
        {
            Expression[] memberAccesses;

            lock (_syncLock)
            {
                _includeTargetNullChecking = targetCanBeNull;

                Visit(expression);

                memberAccesses = _memberAccessesByPath.Values.Reverse().ToArray();

                _stringMemberAccessSubjects.Clear();
                _nullCheckSubjects.Clear();
                _memberAccessesByPath.Clear();
            }

            return memberAccesses;
        }

        protected override Expression VisitBinary(BinaryExpression binary)
        {
            if ((binary.NodeType == ExpressionType.NotEqual) &&
                (binary.Right.NodeType == ExpressionType.Constant) &&
                (((ConstantExpression)binary.Right).Value == null) &&
                ShouldAddNullCheck(binary.Left))
            {
                _nullCheckSubjects.Add(binary.Left.ToString());
            }

            return base.VisitBinary(binary);
        }

        protected override Expression VisitMember(MemberExpression memberAccess)
        {
            if (IsNotRootObject(memberAccess))
            {
                if ((memberAccess.Expression != null) &&
                    (memberAccess.Member.Name == "HasValue") &&
                    (memberAccess.Expression.Type.IsNullableType()))
                {
                    _nullCheckSubjects.Add(memberAccess.Expression.ToString());
                }

                AddStringMemberAccessSubjectIfAppropriate(memberAccess.Expression);
                AddMemberAccessIfAppropriate(memberAccess);
            }

            return base.VisitMember(memberAccess);
        }

        private bool IsNotRootObject(MemberExpression memberAccess)
        {
            if (memberAccess.Member.Name == "Parent")
            {
                return !memberAccess.IsRootedIn(_mappingDataObject);
            }

            if (memberAccess.Expression != _mappingDataObject)
            {
                return true;
            }

            if (memberAccess.Member.Name == "EnumerableIndex")
            {
                return false;
            }

            if (memberAccess.Member.Name == "Source")
            {
                return false;
            }

            return _includeTargetNullChecking || (memberAccess.Member.Name != "Target");
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCall)
        {
            if ((methodCall.Object != _mappingDataObject) &&
                (methodCall.Method.DeclaringType != typeof(IMappingData)))
            {
                AddStringMemberAccessSubjectIfAppropriate(methodCall.Object);
                AddMemberAccessIfAppropriate(methodCall);
            }

            return base.VisitMethodCall(methodCall);
        }

        private void AddStringMemberAccessSubjectIfAppropriate(Expression member)
        {
            if ((member != null) && (member.Type == typeof(string)) && IsNotRootObject(member))
            {
                _stringMemberAccessSubjects.Add(member);
            }
        }

        private bool IsNotRootObject(Expression expression)
        {
            return (expression.NodeType != ExpressionType.MemberAccess) ||
                   IsNotRootObject((MemberExpression)expression);
        }

        private void AddMemberAccessIfAppropriate(Expression memberAccess)
        {
            if (ShouldAddNullCheck(memberAccess))
            {
                _memberAccessesByPath.Add(memberAccess.ToString(), memberAccess);
            }
        }

        private bool ShouldAddNullCheck(Expression memberAccess)
        {
            var memberAccessString = memberAccess.ToString();

            return !_nullCheckSubjects.Contains(memberAccessString) &&
                   ((memberAccess.Type != typeof(string)) || _stringMemberAccessSubjects.Contains(memberAccess)) &&
                   !_memberAccessesByPath.ContainsKey(memberAccessString) &&
                   memberAccess.Type.CanBeNull() &&
                   memberAccess.IsRootedIn(_mappingDataObject);
        }
    }
}