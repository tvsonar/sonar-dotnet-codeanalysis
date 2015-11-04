Namespace Tests.Diagnostics
    Class A ' Noncompliant
        Class B

        End Class
    End Class
    Class C ' Noncompliant

    End Class

    Module D ' Noncompliant
        Interface IB

        End Interface
    End Module
    Interface IA ' Noncompliant
        Interface IB

        End Interface
    End Interface
End Namespace