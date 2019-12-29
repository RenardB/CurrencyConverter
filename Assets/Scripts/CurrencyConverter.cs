using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

//Main component of the application
//Computes the conversion between two currencies depending on the value of the exchange rates at the reference time
//Manage the diffents UI
//Fetches the exchange rates using the API at https://exchangeratesapi.io/ that use European Central Bank reference rates 
//(https://www.ecb.europa.eu/stats/policy_and_exchange_rates/euro_reference_exchange_rates/html/index.en.html#dev)
public class CurrencyConverter : MonoBehaviour
{
    //---- Members ----//
    [Header("Data")]
    [SerializeField, Tooltip("List of supported currencies")]
    private CurrenciesData _currenciesData;

    [Header("SceneRefs")]
    [SerializeField, Tooltip("Displays the date used as reference when computing the exchange rate")]
    private Text _dateDisplay;
    [SerializeField, Tooltip("Displayed when fetching exchange rates")]
    private GameObject _loadingScreen;
    [SerializeField, Tooltip("Displayed if the request fail")]
    private ErrorPopup _errorPopup;
    [SerializeField, Tooltip("Allows to enter the amount we want to convert")]
    private InputField _inputValue;
    [SerializeField, Tooltip("Allows to define the source currency for the conversion")]
    private Dropdown _inputCurrencyDisplay;
    [SerializeField, Tooltip("Allows to define the target currency for the conversion")]
    private Dropdown _outputCurrencyDisplay;
    [SerializeField, Tooltip("Holder of the display of the converted amount")]
    private GameObject _outputValueHolder;
    [SerializeField, Tooltip("Displays the converted amount")]
    private Text _outputValue;

    private DateTime _referenceDate; //< date used as reference for computing the exchange rate
    private DateTime? _requestDate; //< date the fetch coroutine will use as parameter (only reset if the request succeed or manually changed)
    private string _inputCurrency = "EUR"; //< source currency for the conversion
    private string _outputCurrency = "EUR"; //< target currency for the conversion
    private Dictionary<DateTime, Dictionary<string, float>> _exchangeRates = new Dictionary<DateTime, Dictionary<string, float>>(); //< caches all exchange rates; all exchange rates are against Euro
    private Dictionary<string, string> _currenciesFullNames = new Dictionary<string, string>(); //< caches currencies names for dropdowns

    private const string ErrorMessage = "An error occured :(\nCheck your connection and retry";

    //---- Unity functions ----//
    void Start()
    {
        //Init _currenciesFullNames
        _currenciesFullNames = new Dictionary<string, string>();
        foreach (var currency in _currenciesData.Currencies)
            _currenciesFullNames[currency.Symbol] = currency.FullName;

        //Hide output value until we have correct values
        _outputValueHolder.SetActive(false);

        //Get today exchange rate
        _requestDate = DateTime.Today;
        FetchExchangeRates();
    }

    //---- Functions ----//
    //Called by the ErrorPopup when we press retry
    public void FetchExchangeRates()
    {
        StartCoroutine(FetchExchangeRates_Coroutine());
    }

    //Called by the SwapCurrencies button
    public void SwapCurrencies()
    {
        string oldInputCurrency = _inputCurrency;
        _inputCurrency = _outputCurrency;
        _outputCurrency = oldInputCurrency;

        int oldInputCurrencyDisplayValue = _inputCurrencyDisplay.value;
        _inputCurrencyDisplay.value = _outputCurrencyDisplay.value;
        _outputCurrencyDisplay.value = oldInputCurrencyDisplayValue;

        Convert();
    }

    //Called by the AndroidDatePicker component in the Date display
    public void RefreshDate()
    {
        if (DateTime.TryParse(_dateDisplay.text, out DateTime date))
        {
            if (_exchangeRates.ContainsKey(date.Date))
            {
                SetReferenceDate(date, _referenceDate >= DateTime.Today);
                RefreshCurrenciesList();
            }
            else
            {
                _requestDate = date.Date;
                FetchExchangeRates();
            }
        }
        else
            SetReferenceDate(_referenceDate, _referenceDate >= DateTime.Today);
    }

    //Called by the input currency dropdown
    public void RefreshInputCurrency()
    {
        _inputCurrency = Currency.ConvertFullNameToSymbol(_inputCurrencyDisplay.options[_inputCurrencyDisplay.value].text);
        Convert();
    }

    //Called by the output currency dropdown
    public void RefreshOutputCurrency()
    {
        _outputCurrency = Currency.ConvertFullNameToSymbol(_outputCurrencyDisplay.options[_outputCurrencyDisplay.value].text);
        Convert();
    }

    //Called by the input value display (when modified)
    public void Convert()
    {
        if (_exchangeRates.ContainsKey(_referenceDate))
        {
            //All exchange rates are agianst Euro (and EUR is not in the exchange rates dictionnaries)
            float inputExchangeRate = _exchangeRates[_referenceDate].ContainsKey(_inputCurrency) ?
               _exchangeRates[_referenceDate][_inputCurrency] : 
               _inputCurrency == "EUR" ? 1 : 0;
            float outputExchangeRate = _exchangeRates[_referenceDate].ContainsKey(_outputCurrency) ?
                _exchangeRates[_referenceDate][_outputCurrency] :
                _outputCurrency == "EUR" ? 1 : 0;

            if (inputExchangeRate > 0 & outputExchangeRate > 0 && double.TryParse(_inputValue.text, NumberStyles.Any, CultureInfo.InvariantCulture, out double inputValue))
            {
                //Compute conversion and display it
                _outputValueHolder.SetActive(true);
                _outputValue.text = (inputValue * outputExchangeRate / inputExchangeRate).ToString("#,0.##");
                return;
            }
        }

        //If the conversion failed, hide the output value
        _outputValueHolder.SetActive(false);
    }

    //Called by the share button
    //Use a free asset (TODO: check it more thoroughly if we want it for production): https://assetstore.unity.com/packages/tools/integration/native-share-for-android-ios-112731
    public void Share()
    {
        var share = new NativeShare();
        share.SetTitle("Share currency conversion");
        share.SetText(_referenceDate.ToString("d", CultureInfo.CreateSpecificCulture("en-US"))
            + ": " + _inputValue.text + " " + _inputCurrency
            + " = " + _outputValue.text + " " + _outputCurrency);
        share.Share();
    }

    //This function make sure that for each reference date we can only select currencies that have an exchange rate at this date
    private void RefreshCurrenciesList()
    {
        _inputCurrencyDisplay.ClearOptions();
        _outputCurrencyDisplay.ClearOptions();

        int newInputDropdownValue = -1; //< new position of the current input currency in the new options' list
        int newOutputDropdownValue = -1; //< new position of the current output currency in the new options' list

        //Recreate options' list only with currencies that exist both in _currenciesFullNames and the _referenceDate's exchange rate
        var options = new List<string>() { _currenciesFullNames.ContainsKey("EUR") ? _currenciesFullNames["EUR"] : "EUR (Euro)" };
        if (_exchangeRates.ContainsKey(_referenceDate))
            foreach (var exchangeRate in _exchangeRates[_referenceDate])
                if (_currenciesFullNames.ContainsKey(exchangeRate.Key))
                {
                    //get new current input/output currencies position in the options' list
                    if (exchangeRate.Key == _inputCurrency)
                        newInputDropdownValue = options.Count;
                    if (exchangeRate.Key == _outputCurrency)
                        newOutputDropdownValue = options.Count;
                    //Add currency to options' list
                    options.Add(_currenciesFullNames[exchangeRate.Key]);
                }

        //If the current input/output currencies are not found in the new list, reset them to Euro (which is always the first option)
        if (newInputDropdownValue < 0)
        {
            newInputDropdownValue = 0;
            _inputCurrency = "EUR";
        }
        if (newOutputDropdownValue < 0)
        {
            newOutputDropdownValue = 0;
            _outputCurrency = "EUR";
        }

        //Set new options' list and set the correct value for the current input/output currencies
        _inputCurrencyDisplay.AddOptions(options);
        _outputCurrencyDisplay.AddOptions(options);
        _inputCurrencyDisplay.value = newInputDropdownValue;
        _outputCurrencyDisplay.value = newOutputDropdownValue;

        Convert();
    }

    //Fetch all currencies exchange rate at _requestDate, only update _referenceDate if succeeded
    private IEnumerator FetchExchangeRates_Coroutine()
    {
        if (_requestDate == null)
            yield break;

        //Create request
        //The "latest" check is just an additional security, the API is smart enough to give the latest exchange rates if the date given is too high
        DateTime date = _requestDate.Value.Date;
        bool latest = date >= DateTime.Today;
        var request = latest ?
            UnityWebRequest.Get("https://api.exchangeratesapi.io/latest?base=EUR") :
            UnityWebRequest.Get("https://api.exchangeratesapi.io/"
                + date.Year.ToString("D4")
                + "-" + date.Month.ToString("D2")
                + "-" + date.Day.ToString("D2")
                + "?base=EUR");

        //Wait request to finish, the loadingscreen should prevent any input to avoid any conflicts
        _loadingScreen.SetActive(true);
        yield return request.SendWebRequest();
        _loadingScreen.SetActive(false);

        if (!(request.isNetworkError || request.isHttpError))
        {
            //Parse the request result (json)
            var requestMatch = Regex.Match(request.downloadHandler.text, "{((\"rates\":{(?<rates>.*)}|\"date\":\"(?<date>.*)\"|\"base\":\"(?<base>.*)\").*){3}}", RegexOptions.ExplicitCapture);
            //No exception if the keys don't exists, Groups[string] return an empty Group if a key is not found 
            if (requestMatch.Groups["base"].Value == "EUR")
            {
                //The date returned by the request may not be the one requested! (for example if we ask for today's or tomorrow's rates when the last exchange rate on the server are still yesterday's)
                if (DateTime.TryParseExact(requestMatch.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime exchangeRatesDate))
                {
                    //Cache all exchange rates
                    _exchangeRates[exchangeRatesDate] = new Dictionary<string, float>();
                    var exchangeRateMatches = Regex.Matches(requestMatch.Groups["rates"].Value, "\"(?<symbol>[A-Z]*)\":(?<rate>[0-9]*.[0-9]*)", RegexOptions.ExplicitCapture);
                    foreach (Match exchangeRateMatch in exchangeRateMatches)
                        if (float.TryParse(exchangeRateMatch.Groups["rate"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float exchangeRate))
                            _exchangeRates[exchangeRatesDate][exchangeRateMatch.Groups["symbol"].Value] = exchangeRate;
                   
                    if (_exchangeRates[exchangeRatesDate].Count >= 0)
                    {
                        //Everything succeeded, refresh values and displays
                        _requestDate = null; //< reset request date
                        SetReferenceDate(exchangeRatesDate, latest);
                        RefreshCurrenciesList();
                        yield break;
                    }
                    else
                        _exchangeRates.Remove(exchangeRatesDate); //< If no data has been set, remove the dictionnary
                }
                else
                    Debug.LogError("Cannot parse date: " + requestMatch.Groups["date"].Value, this);
            }
            else
                Debug.LogError("Wrong base for exchange rates request: " + requestMatch.Groups["base"].Value, this);
        }
        else
            Debug.LogError(request.error);

        //If something failed, reset display to previous reference date if there is one (<=> the coroutine at least succeeded once <=> _exchangeRates is not empty) and display error popup
        if (_exchangeRates.Count > 0)
            SetReferenceDate(_referenceDate, _referenceDate >= DateTime.Today);
        _errorPopup.Display(ErrorMessage, _exchangeRates.Count > 0);
    }

    //Set _referenceDate and refresh _dateDisplay
    private void SetReferenceDate(DateTime dateTime, bool latest)
    {
        if (_dateDisplay == null)
            return;

        _referenceDate = dateTime.Date;
        _dateDisplay.text = _referenceDate.ToString("d", CultureInfo.CreateSpecificCulture("en-US"));
        if (latest)
            _dateDisplay.text += " (latest)"; //< this mean we have the most up-to-date rates
    }
}