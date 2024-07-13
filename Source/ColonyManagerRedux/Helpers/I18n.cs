// I18n.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyManagerRedux
{
    public class I18n
    {
        public static string HistoryStock = Translate("ColonyManager.HistoryStock");
        public static string HistoryDesignated = Translate("ColonyManager.HistoryDesignated");
        public static string HistoryCorpses = Translate("ColonyManager.HistoryCorpses");
        public static string HistoryChunks = Translate("ColonyManager.HistoryChunks");
        public static string HistoryProduction = Translate("ColonyManager.HistoryProduction");
        public static string HistoryConsumption = Translate("ColonyManager.HistoryConsumption");
        public static string HistoryBatteries = Translate("ColonyManager.HistoryBatteries");


        public static string Aggressiveness(float aggression)
        {
            return Translate("ColonyManager.Aggressiveness",
                              aggression >= .1f
                                  ? aggression.ToStringPercent().Colorize(Color.red)
                                  : aggression.ToStringPercent());
        }

        public static string Key(string key)
        {
            return $"ColonyManagerRedux.{key}";
        }

        public static string Translate(string key, params NamedArgument[] args)
        {
            return Key(key).Translate(args);
        }

        public static string YieldOne(string label)
        {
            return $"{Translate("ColonyManager.Yield")} {label}";
        }

        public static string YieldMany(IEnumerable<string> labels)
        {
            return $"{Translate("ColonyManager.Yield")}\n - {labels.ToLineList(" - ")}";
        }

        public static string YieldOne(float yield, ThingDef def)
        {
            return YieldOne($"{def.LabelCap} x{yield:F0} ");
        }

        public static string Gender(Gender gender)
        {
            return Translate($"Gender.{gender}");
        }

        public static string ChanceToDrop(float chance)
        {
            return Translate("ChanceToDrop", chance.ToStringPercent());
        }
    }
}
