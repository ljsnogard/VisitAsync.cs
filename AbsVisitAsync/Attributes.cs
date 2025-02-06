namespace VisitAsyncUtils
{
    using System;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
    public sealed class AcceptVisitAsyncAttribute : Attribute
    { }
}
