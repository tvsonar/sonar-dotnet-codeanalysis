Public Class ClassWithLogicalStatements

	Public Function [And](first As Boolean, second As Boolean) As Boolean

		If first AndAlso second Then 'Fixed
			Return True
		End If
		Return False

	End Function

	Public Function [AndAlso](first As Boolean, second As Boolean) As Boolean

		If first AndAlso second Then 'Compliant, using AndAlso.
			Return True
		End If
		Return False

	End Function

	Public Function [Or](first As Boolean, second As Boolean) As Boolean

		If first OrElse second Then 'Fixed
			Return True
		End If
		Return False

	End Function

	Public Function [OrElse](first As Boolean, second As Boolean) As Boolean

		If first OrElse second Then 'Compliant, using OrElse.
			Return True
		End If
		Return False

	End Function

End Class
