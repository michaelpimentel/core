﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace eQuantic.Core.Linq
{
    internal class EntitySorterBuilder<T>
    {
        private readonly Type keyType;
        private readonly LambdaExpression keySelector;

        public EntitySorterBuilder(string propertyName)
        {
            //List<MethodInfo> propertyAccessors = GetPropertyAccessors(propertyName);
            //this.keyType = propertyAccessors.Last().ReturnType;

            List<PropertyInfo> properties = GetProperties(propertyName);
            this.keyType = properties.Last().PropertyType;
            var builder = CreateLambdaBuilder(keyType);
            this.keySelector = builder.BuildLambda(properties);
        }

        private interface ILambdaBuilder
        {
            LambdaExpression BuildLambda(IEnumerable<MethodInfo> propertyAccessors);
            LambdaExpression BuildLambda(IEnumerable<PropertyInfo> properties);
        }

        public SortDirection Direction { get; set; }

        public IEntitySorter<T> BuildOrderByEntitySorter()
        {
            Type[] typeArgs = new[] { typeof(T), this.keyType };

            Type sortType =
                typeof(OrderBySorter<,>).MakeGenericType(typeArgs);

            return (IEntitySorter<T>)Activator.CreateInstance(sortType,
                this.keySelector, this.Direction);
        }

        public IEntitySorter<T> BuildThenByEntitySorter(
            IEntitySorter<T> baseSorter)
        {
            Type[] typeArgs = new[] { typeof(T), this.keyType };

            Type sortType =
                typeof(ThenBySorter<,>).MakeGenericType(typeArgs);

            return (IEntitySorter<T>)Activator.CreateInstance(sortType,
                baseSorter, this.keySelector, this.Direction);
        }

        private static ILambdaBuilder CreateLambdaBuilder(Type keyType)
        {
            Type[] typeArgs = new[] { typeof(T), keyType };

            Type builderType =
                typeof(LambdaBuilder<>).MakeGenericType(typeArgs);

            return (ILambdaBuilder)Activator.CreateInstance(builderType);
        }

        private static List<MethodInfo> GetPropertyAccessors(string propertyName)
        {
            try
            {
                return GetPropertyAccessorsFromChain(propertyName);
            }
            catch (InvalidOperationException ex)
            {
                string message = propertyName +
                    " could not be parsed. " + ex.Message;

                // We throw a more expressive exception at this level.
                throw new ArgumentException(message, nameof(propertyName));
            }
        }

        private static List<PropertyInfo> GetProperties(string propertyName)
        {
            try
            {
                return GetPropertiesFromChain(propertyName);
            }
            catch (InvalidOperationException ex)
            {
                string message = propertyName +
                    " could not be parsed. " + ex.Message;

                // We throw a more expressive exception at this level.
                throw new ArgumentException(message, nameof(propertyName));
            }
        }

        private static List<PropertyInfo> GetPropertiesFromChain(string propertyNameChain)
        {
            var properties = new List<PropertyInfo>();

            var declaringType = typeof(T);

            foreach (string name in propertyNameChain.Split('.'))
            {
                var property = GetPropertyByName(declaringType, name);

                properties.Add(property);

                declaringType = property.PropertyType;
            }

            return properties;
        }

        private static List<MethodInfo> GetPropertyAccessorsFromChain(string propertyNameChain)
        {
            var propertyAccessors = new List<MethodInfo>();

            var declaringType = typeof(T);

            foreach (string name in propertyNameChain.Split('.'))
            {
                var accessor = GetPropertyAccessor(declaringType, name);

                propertyAccessors.Add(accessor);

                declaringType = accessor.ReturnType;
            }

            return propertyAccessors;
        }

        private static MethodInfo GetPropertyAccessor(Type declaringType, string propertyName)
        {
            var prop = GetPropertyByName(declaringType, propertyName);

            return GetPropertyGetter(prop);
        }

        private static PropertyInfo GetPropertyByName(Type declaringType, string propertyName)
        {
            BindingFlags flags = BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public;
            var prop = declaringType.GetProperty(propertyName, flags);

            if (prop == null)
            {
                string exceptionMessage = string.Format(
                    "{0} does not contain a property named '{1}'.",
                    declaringType, propertyName);

                throw new InvalidOperationException(exceptionMessage);
            }

            return prop;
        }

        private static MethodInfo GetPropertyGetter(PropertyInfo property)
        {
            
            var propertyAccessor = property.GetMethod;

            if (propertyAccessor == null)
            {
                string exceptionMessage = string.Format(
                    "The property '{1}' does not contain a getter.",
                    property.Name);

                throw new InvalidOperationException(exceptionMessage);
            }

            return propertyAccessor;
        }

        private sealed class LambdaBuilder<TKey> : ILambdaBuilder
        {
            public LambdaExpression BuildLambda(IEnumerable<MethodInfo> propertyAccessors)
            {
                var parameterExpression = Expression.Parameter(typeof(T), "entity");
                var propertyExpression = BuildPropertyExpression(propertyAccessors, parameterExpression);
                return Expression.Lambda<Func<T, TKey>>(propertyExpression, new[] { parameterExpression });
            }

            private static Expression BuildPropertyExpression(IEnumerable<MethodInfo> propertyAccessors,
                ParameterExpression parameterExpression)
            {
                Expression propertyExpression = null;

                foreach (var propertyAccessor in propertyAccessors)
                {
                    var innerExpression = propertyExpression ?? parameterExpression;
                    propertyExpression = Expression.Property(innerExpression, propertyAccessor);
                }

                return propertyExpression;
            }

            private static Expression BuildPropertyExpression(IEnumerable<PropertyInfo> properties,
                ParameterExpression parameterExpression)
            {
                Expression propertyExpression = null;

                foreach (var property in properties)
                {
                    var innerExpression = propertyExpression ?? parameterExpression;

                    propertyExpression = Expression.Property(innerExpression, property);
                }

                return propertyExpression;
            }

            public LambdaExpression BuildLambda(IEnumerable<PropertyInfo> properties)
            {
                var parameterExpression = Expression.Parameter(typeof(T), "entity");
                var propertyExpression = BuildPropertyExpression(properties, parameterExpression);
                return Expression.Lambda<Func<T, TKey>>(propertyExpression, new[] { parameterExpression });
            }
        }
    }
}
