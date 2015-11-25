using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Diagnostics
{
    class Leg
    {
        public int Length { get; set; }
    }

    class Biped
    {
        public Leg LeftLeg { get; set; }
        public Leg RightLeg { get; set; }
    }

    class ParallelCollections
    {
        void Test()
        {
            var rightLegs = new Leg[50];
            var leftLegs = new List<Leg>();

            for (var i = 0; i < rightLegs.Count; i++)
            {
                var rightLeg = rightLegs[rightLegs.Count-i-1];  //Noncompliant
                var rightLeg2 = someOtherCollection[2, rightLegs.Count-i-1];  //Compliant
                var leftLeg = leftLegs[rightLegs.Count - i - 1];    //Noncompliant
                if (leftLeg.Length != rightLeg.Length)
                {
                    //... unlucky
                }
            }

            var creatures = new List<Biped>();
            for (var i = 0; i < creatures.Count; i++)
            {
                var creature = creatures[i];
                var creatureSame = creatures[i];
                if (creature.LeftLeg.Length != creature.RightLeg.Length)
                {
                    //... unlucky
                }
            }

            var dict1 = new Dictionary<int, string>();
            var dict2 = new Dictionary<int, object>();

            foreach (var key in dict1.Keys)
            {
                var s = dict1[key]; //Noncompliant
                s = dict1[key]; //Noncompliant
                var o = dict2[key]; //Noncompliant
                s = dict1[key]; //Noncompliant

                dict2[key] = new object(); //Compliant
            }
        }
    }
}
