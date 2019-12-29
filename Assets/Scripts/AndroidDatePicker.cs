using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

//This component allows to display android's default date picker to modify a Text component displaying a date
//When called (with DisplayDatePicker) it uses the value of the given Text as initial date, and will update this Text and trigger an event when the android date picker is closed
public class AndroidDatePicker : MonoBehaviour
{
    //---- Type definitions ----//
    class DatePickerCallback : AndroidJavaProxy
    {
        private AndroidDatePicker _datePicker;

        public DatePickerCallback(AndroidDatePicker datePicker) : base("android.app.DatePickerDialog$OnDateSetListener") { _datePicker = datePicker; }
        
        void onDateSet(AndroidJavaObject view, int year, int monthOfYear, int dayOfMonth)
        {
            _datePicker._result = new DateTime(year, monthOfYear + 1, dayOfMonth);
        }
    }

    //---- Members ----//
    [SerializeField, Tooltip("Triggered when Android's date picker dialog is closed")]
    private UnityEvent _onDateChanged;

    private AndroidJavaObject _androidActivity; //< manage Android's date picker dialog
    private DateTime? _result; //< result of the date picker dialog
    private Text _target; //< component that requested the display of the date picker dialog

    //---- Unity functions ----//
    private void Update()
    {
        //Update text display and trigger event when the date picker dialog is closed (cannot do it in the callback because of thread problems)
        if (_result != null)
        {
            if (_target != null)
                _target.text = _result?.ToString("d", CultureInfo.CreateSpecificCulture("en-US"));
            _result = null;
            _target = null;
            _onDateChanged?.Invoke();
        }
    }

    //---- Functions ----//
    public void DisplayDatePicker(Text dateDisplay)
    {
        //clean previous data
        _androidActivity?.Dispose();
        _target = dateDisplay;
        _result = null;

        //Get the date currently displayed by dateDisplay
        DateTime date;
        if (dateDisplay == null || !DateTime.TryParse(dateDisplay.text, out date))
            date = DateTime.Today;

        //Open Android's date picker dialog
        _androidActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
        _androidActivity.Call("runOnUiThread", new AndroidJavaRunnable(() => {
            new AndroidJavaObject("android.app.DatePickerDialog", _androidActivity, new DatePickerCallback(this), date.Year, date.Month - 1, date.Day).Call("show");
        }));
    }
}
