using UnityEngine;

public class ViewsManager : MonoBehaviour
{
    [Header("Paneles Principaes")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private GameObject homePanel;

    private void Start()
    {
        ShowHomePanel();
    }

    public void ShowHomePanel()
    {
        loginPanel.SetActive(false);
        registerPanel.SetActive(false);
        homePanel.SetActive(true);
    }

    public void ShowLoginPanel()
    {
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        homePanel.SetActive(false);
    }

    public void ShowRegisterPanel()
    {
        loginPanel.SetActive(false);
        registerPanel.SetActive(true);
        homePanel.SetActive(false);
    }

    public void ActionGoBack()
    {
        ShowHomePanel();
    }
}
