using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//Simple component to display an error popup
public class ErrorPopup : MonoBehaviour
{
    //---- Members ----//
    [SerializeField]
    private Text _messageLabel;
    [SerializeField]
    private GameObject _cancelButton;

    //---- Functions ----//
    public void Display(string message, bool cancelable)
    {
        gameObject.SetActive(true);
        _messageLabel.text = message;
        _cancelButton.SetActive(cancelable);
    }
}
