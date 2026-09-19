using Microsoft.Extensions.VectorData;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace ContextDepot.Infrastructure.VectorStore;

internal static class PostgreSqlVectorFilterTranslator
{
    public static PostgreSqlVectorFilterTranslation Translate<TRecord>(
        Expression<Func<TRecord, bool>>? filter,
        IReadOnlyList<VectorStoreProperty> properties)
    {
        if (filter is null)
        {
            return new PostgreSqlVectorFilterTranslation("TRUE", []);
        }

        var parameters = new List<(string Name, object? Value)>();
        var sql = TranslateExpression(filter.Body, filter.Parameters[0], properties, parameters);
        return new PostgreSqlVectorFilterTranslation(sql, parameters);
    }

    private static string TranslateExpression(
        Expression expression,
        ParameterExpression recordParameter,
        IReadOnlyList<VectorStoreProperty> properties,
        ICollection<(string Name, object? Value)> parameters)
    {
        expression = UnwrapConvert(expression);

        if (expression is BinaryExpression { NodeType: ExpressionType.AndAlso or ExpressionType.And } conjunction)
        {
            var left = TranslateExpression(conjunction.Left, recordParameter, properties, parameters);
            var right = TranslateExpression(conjunction.Right, recordParameter, properties, parameters);
            return $"({left}) AND ({right})";
        }

        if (expression is BinaryExpression { NodeType: ExpressionType.Equal } equality)
        {
            var member = FindRecordMember(equality.Left, recordParameter) ?? FindRecordMember(equality.Right, recordParameter);
            if (member is null)
            {
                throw Unsupported(expression);
            }

            var valueExpression = ReferenceEquals(member, UnwrapConvert(equality.Left))
                ? equality.Right
                : equality.Left;
            var value = Evaluate(valueExpression);
            var property = FindProperty(member.Member.Name, properties);
            EnsureFilterable(property);
            var column = PostgreSqlVectorSqlBuilder.QuoteIdentifier(PostgreSqlVectorSqlBuilder.GetStorageName(property));

            if (value is null)
            {
                return $"{column} IS NULL";
            }

            var name = $"filter_{parameters.Count}";
            parameters.Add((name, value));
            return $"{column} = @{name}";
        }

        if (expression is MethodCallExpression methodCall &&
            string.Equals(methodCall.Method.Name, "Contains", StringComparison.Ordinal))
        {
            return TranslateContains(methodCall, recordParameter, properties, parameters);
        }

        throw Unsupported(expression);
    }

    private static string TranslateContains(
        MethodCallExpression methodCall,
        ParameterExpression recordParameter,
        IReadOnlyList<VectorStoreProperty> properties,
        ICollection<(string Name, object? Value)> parameters)
    {
        Expression? valuesExpression;
        Expression? memberExpression;
        if (methodCall.Object is not null && methodCall.Arguments.Count == 1)
        {
            valuesExpression = methodCall.Object;
            memberExpression = methodCall.Arguments[0];
        }
        else if (methodCall.Object is null && methodCall.Arguments.Count == 2)
        {
            valuesExpression = methodCall.Arguments[0];
            memberExpression = methodCall.Arguments[1];
        }
        else
        {
            throw Unsupported(methodCall);
        }

        var member = FindRecordMember(memberExpression, recordParameter);
        if (member is null)
        {
            throw Unsupported(methodCall);
        }

        var property = FindProperty(member.Member.Name, properties);
        EnsureFilterable(property);
        var values = Evaluate(valuesExpression) as IEnumerable;
        if (values is null || values is string)
        {
            throw Unsupported(methodCall);
        }

        var materializedValues = values.Cast<object?>().ToArray();
        if (materializedValues.Length == 0)
        {
            return "FALSE";
        }

        var column = PostgreSqlVectorSqlBuilder.QuoteIdentifier(PostgreSqlVectorSqlBuilder.GetStorageName(property));
        var names = materializedValues.Select((value, index) =>
        {
            var name = $"filter_{parameters.Count + index}";
            parameters.Add((name, value));
            return $"@{name}";
        });

        return $"{column} IN ({string.Join(", ", names)})";
    }

    private static VectorStoreProperty FindProperty(string name, IReadOnlyList<VectorStoreProperty> properties) =>
        properties.FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.Ordinal))
        ?? throw new NotSupportedException($"The vector filter references unknown property '{name}'.");

    private static void EnsureFilterable(VectorStoreProperty property)
    {
        if (property is not VectorStoreKeyProperty and not VectorStoreDataProperty)
        {
            throw new NotSupportedException($"The vector filter property '{property.Name}' is not filterable.");
        }

        if (property.Type != typeof(Guid) && property.Type != typeof(string))
        {
            throw new NotSupportedException($"The vector filter property '{property.Name}' has an unsupported type.");
        }
    }

    private static MemberExpression? FindRecordMember(Expression expression, ParameterExpression recordParameter)
    {
        expression = UnwrapConvert(expression);
        return expression is MemberExpression member &&
            member.Expression == recordParameter &&
            member.Member is PropertyInfo
            ? member
            : null;
    }

    private static object? Evaluate(Expression expression)
    {
        expression = UnwrapConvert(expression);
        if (expression is ConstantExpression constant)
        {
            return constant.Value;
        }

        if (expression is MemberExpression { Expression: not null } member &&
            !ContainsParameter(member))
        {
            return Expression.Lambda(expression).Compile().DynamicInvoke();
        }

        if (!ContainsParameter(expression))
        {
            return Expression.Lambda(expression).Compile().DynamicInvoke();
        }

        throw Unsupported(expression);
    }

    private static bool ContainsParameter(Expression expression) =>
        expression switch
        {
            ParameterExpression => true,
            MemberExpression member when member.Expression is not null => ContainsParameter(member.Expression),
            UnaryExpression unary => ContainsParameter(unary.Operand),
            MethodCallExpression call =>
                (call.Object is not null && ContainsParameter(call.Object)) || call.Arguments.Any(ContainsParameter),
            _ => false
        };

    private static Expression UnwrapConvert(Expression expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? UnwrapConvert(unary.Operand)
            : expression;

    private static NotSupportedException Unsupported(Expression expression) =>
        new($"The vector filter expression '{expression}' is not supported by the PostgreSQL provider.");
}
