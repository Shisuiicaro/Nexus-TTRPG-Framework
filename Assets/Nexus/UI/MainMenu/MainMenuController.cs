using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float animationDelay = 0.1f;
    [SerializeField] private float initialDelay = 0.5f;
    [SerializeField] private Sprite logoSprite;
    private UIDocument _doc;
    private VisualElement _root;
    

    private VisualElement _logoSection;
    private VisualElement _footerSection;
    private List<Button> _menuButtons = new List<Button>();

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;


        _logoSection = _root.Q<VisualElement>("LogoSection");
        _footerSection = _root.Q<VisualElement>("Footer");

        var logo = _root.Q<Image>("Logo");
        if (logo != null && logoSprite != null)
        {
            logo.sprite = logoSprite;
            logo.scaleMode = ScaleMode.ScaleToFit;
        }

        var titleLabel = _root.Q<Label>("ProjectTitle");





        var menuContainer = _root.Q<VisualElement>("MainMenu");
        menuContainer.Query<Button>().ForEach(btn => _menuButtons.Add(btn));

        foreach (var btn in _menuButtons)
        {
            var accent = btn.Q<VisualElement>(null, "btn-accent");
            var deco = btn.Q<VisualElement>(null, "btn-decoration");
            if (accent != null) accent.style.scale = new Scale(new Vector3(1f, 0f, 1f));
            if (deco != null) deco.style.opacity = 0;

            btn.RegisterCallback<PointerEnterEvent>(e =>
            {
                if (accent != null) accent.style.scale = new Scale(new Vector3(1f, 1f, 1f));
                if (deco != null) deco.style.opacity = 1;
            });

            btn.RegisterCallback<PointerLeaveEvent>(e =>
            {
                if (accent != null) accent.style.scale = new Scale(new Vector3(1f, 0f, 1f));
                if (deco != null) deco.style.opacity = 0;
            });
        }


        _root.Q<Button>("SoloBtn").clicked += () => OnMenuClick("Solo");
        _root.Q<Button>("MultiplayerBtn").clicked += () => OnMenuClick("Multiplayer");
        _root.Q<Button>("ConfigBtn").clicked += () => OnMenuClick("Config");
        _root.Q<Button>("CreditsBtn").clicked += () => OnMenuClick("Credits");
        _root.Q<Button>("QuitBtn").clicked += OnQuitClick;


        StartCoroutine(AnimateEntrance());
    }



    private IEnumerator AnimateEntrance()
    {

        _logoSection.style.opacity = 0;
        _logoSection.style.translate = new Translate(0, -20, 0);
        _footerSection.style.opacity = 0;
        foreach (var btn in _menuButtons)
        {
            btn.style.opacity = 0;
            btn.style.translate = new Translate(-20, 0, 0);
        }

        yield return new WaitForSeconds(initialDelay);


        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            _logoSection.style.opacity = smoothT;
            _logoSection.style.translate = new Translate(0, Mathf.Lerp(-20, 0, smoothT), 0);
            yield return null;
        }
        _logoSection.style.opacity = 1;
        _logoSection.style.translate = new Translate(0, 0, 0);

        yield return new WaitForSeconds(0.2f);


        for (int i = 0; i < _menuButtons.Count; i++)
        {
            StartCoroutine(AnimateButton(_menuButtons[i], i * 0.1f));
        }

        yield return new WaitForSeconds(0.5f);


        t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            _footerSection.style.opacity = Mathf.SmoothStep(0, 1, t);
            yield return null;
        }
        _footerSection.style.opacity = 1;
    }

    private IEnumerator AnimateButton(Button btn, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);

        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            btn.style.opacity = smoothT;
            btn.style.translate = new Translate(Mathf.Lerp(-20, 0, smoothT), 0, 0);
            yield return null;
        }
        btn.style.opacity = 1;
        btn.style.translate = new Translate(0, 0, 0);
    }

    private void OnMenuClick(string action)
    {
        Debug.Log($"Menu Action: {action}");

    }

    private void OnQuitClick()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
