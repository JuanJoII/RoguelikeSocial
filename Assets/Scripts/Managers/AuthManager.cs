using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Supabase;
using Supabase.Gotrue;
using Unity.VisualScripting;
using UnityEngine;

public class AuthManager : MonoBehaviour
{
    [Header("Config Supabase")]
    [SerializeField] private string supabaseUrl = "URL_Supabase";
    [SerializeField] private string supabaseAnonKey = "KEY_Supabase";

    public Supabase.Client SupabaseClient { get; private set; }

    private void Awake()
    {
        var options = new SupabaseOptions
        {
            AutoConnectRealtime = true,
            AutoRefreshToken = true
        };

        SupabaseClient = new Supabase.Client(supabaseUrl, supabaseAnonKey, options);
    }

    private async void Start()
    {
        try
        {
            await SupabaseClient.InitializeAsync().AsUniTask();
            Debug.Log("Supabase client initialized successfully.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to initialize Supabase client: {ex.Message}");
        }
    }

    public async UniTask<bool> LoginAsync(string email, string password)
    {
        try
        {
            Debug.Log("Attempting to log in...");
            var session = await SupabaseClient.Auth.SignIn(email, password).AsUniTask();
            if (session != null && session.User != null)
            {
                Debug.Log($"Login successful! User ID: {session.User.Id}");
                return true;
            }

            Debug.LogWarning("Login failed: No session or user returned.");
            return false;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Login failed: {ex.Message}");
            return false;
        }
    }

    public async UniTask<bool> RegisterAsync(string email, string password, string username)
    {
        try
        {
            Debug.Log("Attempting to register...");
            var options = new SignUpOptions
            {
                Data = new Dictionary<string, object>
                {
                    { "username", username }
                }
            };

            var session = await SupabaseClient.Auth.SignUp(email, password, options).AsUniTask();

            if (session != null && session.User != null)
            {
                Debug.Log($"Registration successful! User ID: {session.User.Id}");
                return true;
            }

            return false;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Registration failed: {ex.Message}");
            return false;
        }
    }
}
