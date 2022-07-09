using System;
using System.Collections.Generic;
using System.Text;

namespace XSplitScreen.Wrappers
{
    class RiskOfOptionsWrapper
    {
        private static bool? _enabled;

        public static bool Enabled
        {
            get
            {
                if(_enabled == null)
                {
                    _enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.run580.riskofoptions");
                }

                return (bool)_enabled;
            }
        }
    }
}
