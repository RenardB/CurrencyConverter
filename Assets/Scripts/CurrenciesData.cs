using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Contains a list of the different currencies (symbol + name) supported by the app
//TODO: currently it's hardcoded because the API I use for the exchange rates cannot provide the currencies full names, 
//but with the correct API these infos could be also set/updated by query
[CreateAssetMenu(fileName = "CurrenciesData", menuName = "ScriptableObjects/CurrenciesData", order = 1)]
public class CurrenciesData : ScriptableObject
{
    [SerializeField, Tooltip("To support more currencies, add them here")]
    private Currency[] _currencies;

    public Currency[] Currencies { get { return _currencies; } }
}

//Contains different infos about a currency and some methods to process these infos
[System.Serializable]
public struct Currency
{
    [Tooltip("Symbol used for the exchange rate queries with the API")]
    public string Symbol;
    [Tooltip("Real name of the currency")]
    public string Name;
    public string FullName { get { return Symbol + " (" + Name + ")"; } }
    static public string ConvertFullNameToSymbol(string fullName)
    {
        return string.IsNullOrEmpty(fullName) ? "" : fullName.Split(' ')[0];
    }
}