using System;
using System.Reflection;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Helper methods for manipulating objects using reflection
    /// </summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Uses reflection to load the value of field <paramref name="fieldName"/>
        /// from <paramref name="from"/> and then uses the value to set the same
        /// field on <paramref name="to"/>.
        /// </summary>
        public static void CopyFieldFrom(object to, string fieldName, object from)
        {
            FieldInfo field;
            try
            {
                field = from.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.GetField | BindingFlags.Public | BindingFlags.NonPublic);

                if (null == field)
                    throw new Exception("GetField() returned null");
            }
            catch (Exception e)
            {
                throw new Exception(
                    $"Did not find a field [{fieldName}] on type [{from.GetType().FullName}]: {e.Message}", e);
            }

            object fromFieldValue;
            try
            {
                fromFieldValue = field.GetValue(from);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to get [{from.GetType().FullName}.{fieldName}] from [from] which is of type []: " + e.Message, e);
            }

            try
            {
                field.SetValue(to, fromFieldValue);
            }
            catch (Exception e)
            {
                throw new Exception(
                    $"Failed setting [{to.GetType().FullName}.{fieldName}] with value of type " +
                    $"[{fromFieldValue?.GetType().Name ?? "null"}]: {e.Message}", e);
            }
        }

        /// <summary>
        /// Uses reflection to set a (private) field <paramref name="fieldName"/>
        /// on <paramref name="o"/>.
        /// </summary>
        /// <returns>
        /// Returns <paramref name="o"/> so that this method can be called
        /// fluently
        /// </returns>
        public static T SetField<T>(T o, string fieldName, object value)
        {
            FieldInfo field;
            try
            {
                field = o.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.GetField | BindingFlags.Public | BindingFlags.NonPublic);

                if (null == field)
                    throw new Exception("GetField() returned null");
            }
            catch (Exception e)
            {
                throw new Exception(
                    $"Did not find a field [{fieldName}] on type [{o.GetType().Name}]: {e.Message}", e);
            }

            try
            {
                field.SetValue(o, value);
            }
            catch (Exception e)
            {
                throw new Exception(
                    $"Failed setting [{o.GetType().Name}.{fieldName}] with value of type " +
                    $"[{value?.GetType().Name ?? "null"}]: {e.Message}", e);
            }

            return o;
        }

        /// <summary>
        /// Uses reflection to load the value of property <paramref name="propertyName"/>
        /// from <paramref name="from"/> and then uses the value to set the same
        /// property on <paramref name="to"/>.
        /// </summary>
        public static void CopyPropertyFrom(object to, string propertyName, object from)
        {
            PropertyInfo property;
            try
            {
                property = from.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.GetProperty  | BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.NonPublic);

                if (null == property)
                    throw new Exception("GetProperty() returned null");
            }
            catch (Exception e)
            {
                throw new Exception(
                    $"Did not find a property [{propertyName}] on type [{from.GetType().FullName}]: {e.Message}", e);
            }

            object fromPropertyValue;
            try
            {
                fromPropertyValue = property.GetValue(from, null);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to get [{from.GetType().FullName}.{propertyName}] from [from] which is of type []: " + e.Message, e);
            }

            try
            {
                property.SetValue(to, fromPropertyValue, null);
            }
            catch (Exception e)
            {
                throw new Exception(
                    $"Failed setting [{to.GetType().FullName}.{propertyName}] with value of type " +
                    $"[{fromPropertyValue?.GetType().Name ?? "null"}]: {e.Message}", e);
            }
        }
    }
}
