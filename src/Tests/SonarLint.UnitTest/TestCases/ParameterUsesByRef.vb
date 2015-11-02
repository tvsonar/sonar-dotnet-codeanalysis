Module Module1
    Sub Foo(ByRef result As Integer, ' Noncompliant
            ByRef result2 As Integer, ' Noncompliant
            ByVal result3 As Integer)
        result = 42
    End Sub

    Function Foo2() As Integer        ' Compliant
        Return 42
    End Function

    Sub Main()
        Dim result As Integer
        Foo(result)
        Console.WriteLine(result)
    End Sub
End Module