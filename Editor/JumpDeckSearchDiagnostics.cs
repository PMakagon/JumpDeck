using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Search;

namespace JumpDeck.Editor
{
    // Unity 2023.2 can record provider errors publicly but has no public error reader.
    // Isolate this optional diagnostic bridge; search itself uses the supported API.
    internal static class JumpDeckSearchDiagnostics
    {
        private static readonly MethodInfo ReadErrors = typeof(SearchContext).GetMethod(
            "GetAllErrors", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static SearchQueryError[] GetErrors(SearchContext context)
        {
            if (ReadErrors == null) return Array.Empty<SearchQueryError>();
            try
            {
                return (ReadErrors.Invoke(context, null) as IEnumerable<SearchQueryError>)?.ToArray()
                    ?? Array.Empty<SearchQueryError>();
            }
            catch (TargetInvocationException) { return Array.Empty<SearchQueryError>(); }
            catch (MemberAccessException) { return Array.Empty<SearchQueryError>(); }
        }
    }
}
