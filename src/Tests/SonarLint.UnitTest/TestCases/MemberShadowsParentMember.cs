using System;

namespace Tests.TestCases
{
    public interface IInterface
    {
        void SomeMethod();
    }

    public class Fruit : IInterface
    {
        protected int ripe;
        protected int flesh;

        // ...
        private int flesh_color;
        public double GetCost(int i)
        {
            return 3.5;
        }

        private double GetCost2()
        {
            return 3.5;
        }
        public virtual int MyProperty { get; set; }

        public void Equals() { } //Compliant, although Object.Equals is conflicting
        public void SomeMethod() { } //Compliant, coming from the interface
    }

    public class Raspberry : Fruit
    {
        private bool ripe;  // Noncompliant
        private static int FLESH; // Noncompliant
        private static int FLESH_COLOR;

        public new double GetCost(int i)  // Noncompliant
        {
            return 7.5;
        }
        public double GetCost(uint i) // Noncompliant
        {
            return 7.5;
        }


        private new double GetCost2()
        {
            return 7.5;
        }
        public new int myproperty { get; set; } //Noncompliant
    }
}
