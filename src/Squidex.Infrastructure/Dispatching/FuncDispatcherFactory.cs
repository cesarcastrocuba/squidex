// ==========================================================================
//  FuncDispatcherFactory.cs
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex Group
//  All rights reserved.
// ==========================================================================

using System;
using System.Reflection;

// ReSharper disable once UnusedMember.Local

namespace Squidex.Infrastructure.Dispatching
{
    internal static class FuncDispatcherFactory
    {
        public static Tuple<Type, Func<TTarget, object, TReturn>> CreateFuncHandler<TTarget, TReturn>(MethodInfo methodInfo)
        {
            var inputType = methodInfo.GetParameters()[0].ParameterType;

            var factoryMethod =
                typeof(FuncDispatcherFactory)
                    .GetMethod("Factory", BindingFlags.Static | BindingFlags.NonPublic)
                    .MakeGenericMethod(typeof(TTarget), inputType, methodInfo.ReturnType);

            var handler = factoryMethod.Invoke(null, new object[] { methodInfo });

            return new Tuple<Type, Func<TTarget, object, TReturn>>(inputType, (Func<TTarget, object, TReturn>)handler);
        }

        private static Func<TTarget, object, TReturn> Factory<TTarget, TIn, TReturn>(MethodInfo methodInfo)
        {
            var type = typeof(Func<TTarget, TIn, TReturn>);

            var handler = (Func<TTarget, TIn, TReturn>)methodInfo.CreateDelegate(type);

            return (target, input) => handler(target, (TIn)input);
        }
    }
}