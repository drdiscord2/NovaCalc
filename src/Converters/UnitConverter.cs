using System;
using System.Collections.Generic;

namespace NovaCalc.Converters
{
    /// <summary>Одна единица измерения: имя (ключ локализации) и коэффициент к базовой единице категории.</summary>
    public class Unit
    {
        public string Key;          // уникальный код, напр. "m"
        public string NameKey;      // отображаемое имя (английское — используется как подпись)
        public decimal Factor;      // 1 <unit> = Factor * baseUnit
        public Func<decimal, decimal> ToBase;    // необязательные нелинейные преобразования (температура)
        public Func<decimal, decimal> FromBase;

        public Unit(string key, string nameKey, decimal factor)
        {
            Key = key; NameKey = nameKey; Factor = factor;
        }

        public Unit(string key, string nameKey, Func<decimal, decimal> toBase, Func<decimal, decimal> fromBase)
        {
            Key = key; NameKey = nameKey; ToBase = toBase; FromBase = fromBase; Factor = 1m;
        }

        public decimal ConvertToBase(decimal v)
        {
            if (ToBase != null) return ToBase(v);
            return v * Factor;
        }

        public decimal ConvertFromBase(decimal v)
        {
            if (FromBase != null) return FromBase(v);
            return v / Factor;
        }
    }

    /// <summary>Категория единиц.</summary>
    public class UnitCategory
    {
        public string Key;
        public string NameKey;
        public List<Unit> Units = new List<Unit>();

        public Unit Find(string unitKey)
        {
            foreach (var u in Units) if (u.Key == unitKey) return u;
            return null;
        }
    }

    /// <summary>
    /// Конвертер единиц измерения: длина, масса, температура, объём, площадь, скорость,
    /// время, давление, энергия, мощность, данные. Всё на decimal, линейные коэффициенты точны.
    /// </summary>
    public static class UnitConverter
    {
        private static readonly List<UnitCategory> Categories = Build();

        public static List<UnitCategory> All { get { return Categories; } }

        public static UnitCategory Category(string key)
        {
            foreach (var c in Categories) if (c.Key == key) return c;
            return Categories[0];
        }

        /// <summary>Конвертирует значение из одной единицы категории в другую.</summary>
        public static decimal Convert(string categoryKey, string fromUnit, string toUnit, decimal value)
        {
            var cat = Category(categoryKey);
            var from = cat.Find(fromUnit);
            var to = cat.Find(toUnit);
            if (from == null || to == null) throw new FormatException("unknown unit");
            decimal baseV = from.ConvertToBase(value);
            return to.ConvertFromBase(baseV);
        }

        private static UnitCategory Cat(string key, string nameKey, params Unit[] units)
        {
            var c = new UnitCategory { Key = key, NameKey = nameKey };
            c.Units.AddRange(units);
            return c;
        }

        private static List<UnitCategory> Build()
        {
            return new List<UnitCategory>
            {
                // Длина (база: метр)
                Cat("length", "Length",
                    new Unit("m", "Meter", 1m),
                    new Unit("km", "Kilometer", 1000m),
                    new Unit("cm", "Centimeter", 0.01m),
                    new Unit("mm", "Millimeter", 0.001m),
                    new Unit("um", "Micrometer", 0.000001m),
                    new Unit("nm", "Nanometer", 0.000000001m),
                    new Unit("mi", "Mile", 1609.344m),
                    new Unit("yd", "Yard", 0.9144m),
                    new Unit("ft", "Foot", 0.3048m),
                    new Unit("in", "Inch", 0.0254m),
                    new Unit("nmi", "Nautical mile", 1852m)),

                // Масса (база: килограмм)
                Cat("mass", "Mass",
                    new Unit("kg", "Kilogram", 1m),
                    new Unit("g", "Gram", 0.001m),
                    new Unit("mg", "Milligram", 0.000001m),
                    new Unit("t", "Tonne", 1000m),
                    new Unit("lb", "Pound", 0.45359237m),
                    new Unit("oz", "Ounce", 0.028349523125m),
                    new Unit("st", "Stone", 6.35029318m),
                    new Unit("car", "Carat", 0.0002m)),

                // Температура (нелинейная)
                Cat("temperature", "Temperature",
                    new Unit("C", "\u00B0C", v => v, v => v),
                    new Unit("F", "\u00B0F", v => (v - 32m) * 5m / 9m, v => v * 9m / 5m + 32m),
                    new Unit("K", "K", v => v - 273.15m, v => v + 273.15m)),

                // Объём (база: литр)
                Cat("volume", "Volume",
                    new Unit("l", "Liter", 1m),
                    new Unit("ml", "Milliliter", 0.001m),
                    new Unit("m3", "Cubic meter", 1000m),
                    new Unit("gal_us", "Gallon (US)", 3.785411784m),
                    new Unit("gal_uk", "Gallon (UK)", 4.54609m),
                    new Unit("qt", "Quart (US)", 0.946352946m),
                    new Unit("pt", "Pint (US)", 0.473176473m),
                    new Unit("cup", "Cup (US)", 0.24m),
                    new Unit("floz", "Fluid ounce (US)", 0.0295735295625m),
                    new Unit("cm3", "Cubic centimeter", 0.001m)),

                // Площадь (база: квадратный метр)
                Cat("area", "Area",
                    new Unit("m2", "Square meter", 1m),
                    new Unit("km2", "Square kilometer", 1000000m),
                    new Unit("cm2", "Square centimeter", 0.0001m),
                    new Unit("ha", "Hectare", 10000m),
                    new Unit("are", "Are", 100m),
                    new Unit("acre", "Acre", 4046.8564224m),
                    new Unit("ft2", "Square foot", 0.09290304m),
                    new Unit("in2", "Square inch", 0.00064516m),
                    new Unit("yd2", "Square yard", 0.83612736m)),

                // Скорость (база: м/с)
                Cat("speed", "Speed",
                    new Unit("mps", "m/s", 1m),
                    new Unit("kph", "km/h", 1m / 3.6m),
                    new Unit("mph", "mph", 0.44704m),
                    new Unit("kn", "Knot", 0.5144444444444444444444444444m),
                    new Unit("cms", "cm/s", 0.01m),
                    new Unit("fps", "ft/s", 0.3048m)),

                // Время (база: секунда)
                Cat("time", "Time",
                    new Unit("s", "Second", 1m),
                    new Unit("ms", "Millisecond", 0.001m),
                    new Unit("us", "Microsecond", 0.000001m),
                    new Unit("min", "Minute", 60m),
                    new Unit("h", "Hour", 3600m),
                    new Unit("day", "Day", 86400m),
                    new Unit("week", "Week", 604800m),
                    new Unit("month30", "Month (30 days)", 2592000m),
                    new Unit("year", "Year (365 days)", 31536000m)),

                // Давление (база: паскаль)
                Cat("pressure", "Pressure",
                    new Unit("pa", "Pascal", 1m),
                    new Unit("kpa", "Kilopascal", 1000m),
                    new Unit("bar", "Bar", 100000m),
                    new Unit("mbar", "Millibar", 100m),
                    new Unit("atm", "Atmosphere", 101325m),
                    new Unit("psi", "PSI", 6894.757293168361m),
                    new Unit("mmhg", "mmHg", 133.322387415m)),

                // Энергия (база: джоуль)
                Cat("energy", "Energy",
                    new Unit("j", "Joule", 1m),
                    new Unit("kj", "Kilojoule", 1000m),
                    new Unit("cal", "Calorie", 4.184m),
                    new Unit("kcal", "Kilocalorie", 4184m),
                    new Unit("wh", "Watt-hour", 3600m),
                    new Unit("kwh", "Kilowatt-hour", 3600000m),
                    new Unit("btu", "BTU", 1055.05585262m),
                    new Unit("ev", "Electronvolt", 0.0000000000000000001602176634m)),

                // Мощность (база: ватт)
                Cat("power", "Power",
                    new Unit("w", "Watt", 1m),
                    new Unit("kw", "Kilowatt", 1000m),
                    new Unit("mw_unit", "Megawatt", 1000000m),
                    new Unit("hp", "Horsepower (metric)", 735.49875m),
                    new Unit("hp_imp", "Horsepower (imperial)", 745.69987158227022m)),

                // Данные (база: байт)
                Cat("data", "Data",
                    new Unit("b", "Bit", 0.125m),
                    new Unit("byte", "Byte", 1m),
                    new Unit("kb", "Kilobyte (1000)", 1000m),
                    new Unit("kib", "Kibibyte (1024)", 1024m),
                    new Unit("mb", "Megabyte", 1000000m),
                    new Unit("mib", "Mebibyte", 1048576m),
                    new Unit("gb", "Gigabyte", 1000000000m),
                    new Unit("gib", "Gibibyte", 1073741824m),
                    new Unit("tb", "Terabyte", 1000000000000m),
                    new Unit("pb", "Petabyte", 1000000000000000m)),
            };
        }
    }
}
