namespace Tests.Diagnostics
{
    class A // Noncompliant
    {
        class B { }
    }
    class C { } // Noncompliant

    interface IA // Noncompliant
    {
        interface IB { }
    }
}
