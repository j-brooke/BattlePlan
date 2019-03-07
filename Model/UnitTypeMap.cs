using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePlan.Model
{
    /// <summary>
    /// Convenient bundle of UnitCharacterists indexed by name.
    /// </summary>
    public class UnitTypeMap
    {
        public IList<UnitCharacteristics> AsList => _unitsList;

        public UnitTypeMap(IEnumerable<UnitCharacteristics> unitCharList)
        {
            _unitsList = unitCharList.ToList().AsReadOnly();
            _unitsMap = new Dictionary<string, UnitCharacteristics>();
            foreach (var unitDef in unitCharList)
                _unitsMap[unitDef.Name] = unitDef;
        }

        public UnitCharacteristics Get(string name)
        {
            UnitCharacteristics val = null;
            _unitsMap.TryGetValue(name, out val);
            return val;
        }

        private Dictionary<string,UnitCharacteristics> _unitsMap;
        private IList<UnitCharacteristics> _unitsList;
    }
}
