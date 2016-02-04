using System;

namespace Tests.TestCases
{
    class CatchRethrow
    {
        private void doSomething(  ) { throw new NotSupportedException()  ; }
        public void Test()
        {
            var someWronglyFormatted =      45     ;
            doSomething();
            doSomething();
            doSomething();

            try
            {
                doSomething();
            }
            catch (ArgumentException)
            {
                Console.WriteLine("");
                throw;
            }
            catch (Exception)
            {
                Console.WriteLine("");
                throw;
            }

            try
            {
                doSomething();
            }
            finally
            {

            }

            try
            {
                doSomething();
            }
            catch (ArgumentException)
            {
                //some comment to make it compliant
                throw;
            }
        }
    }
}
