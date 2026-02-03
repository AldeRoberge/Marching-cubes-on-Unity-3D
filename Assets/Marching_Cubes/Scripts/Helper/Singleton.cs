using UnityEngine;

/// <summary>
/// Singleton. See <a href="https://github.com/UnityCommunity/UnitySingleton">Unity Singleton</a>
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : Component
{
    #region Fields

    /// <summary>
    /// The instance.
    /// </summary>
    private static T _instance;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the instance.
    /// </summary>
    /// <value>The instance.</value>
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject();
                    obj.name = typeof(T).Name;
                    _instance = obj.AddComponent<T>();
                    //DontDestroyOnLoad(instance);
                }
            }

            return _instance;
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Use this for initialization.
    /// </summary>
    public virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //Destroy singleton instance on destroy
    public void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>
    /// Check if singleton is already created
    /// </summary>
    public static bool IsCreated()
    {
        return (_instance != null);
    }

    #endregion
}