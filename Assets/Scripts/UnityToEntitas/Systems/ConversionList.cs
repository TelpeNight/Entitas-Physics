using System.Collections.Generic;
using UnityToEntitas.Behaviours;

namespace UnityToEntitas.Systems
{
    public static class ConversionList
    {
        private static readonly List<ConvertToEntitas> List = new List<ConvertToEntitas>();
        
        public static void Add(ConvertToEntitas component) {
            List.Add(component);
        }
                
        public static List<ConvertToEntitas> PickConversionQueue()
        {
            List<ConvertToEntitas> queue = List.FindAll(c => c != null);
            List.Clear();
            return queue;
        }
    }
}