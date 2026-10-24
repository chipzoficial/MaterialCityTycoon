using MaterialCityTycoon.Building;

namespace MaterialCityTycoon.Commerce
{
    public static class DisplaySlotCompatibility
    {
        public static bool IsCompatible(ProductDefinition productDefinition, DisplaySlotDefinition slotDefinition)
        {
            if (productDefinition == null
                || slotDefinition == null
                || !productDefinition.IsValid(out _)
                || !slotDefinition.IsValid(out _))
            {
                return false;
            }

            foreach (ProductDisplayMode productMode in productDefinition.AllowedDisplayModes)
            {
                foreach (ProductDisplayMode slotMode in slotDefinition.AllowedDisplayModes)
                {
                    if (productMode == slotMode)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
