using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine.SceneManagement;

[Serializable]
public class AnimKeyframe
{
    public float tempo;
    public string propriedade;
    public float valor;
    [NonSerialized]
    public GameObject targetObject;

    public AnimKeyframe(float t, string prop, float val, GameObject obj)
    {
        tempo = t;
        propriedade = prop;
        valor = val;
        targetObject = obj;
    }
}

[Serializable]
public class AnimationTrack
{
    [NonSerialized]
    public GameObject gameObject;
    public List<AnimKeyframe> keyframes = new List<AnimKeyframe>();
    public bool expandido = true;

    public AnimationTrack(GameObject obj)
    {
        gameObject = obj;
    }
}

[Serializable]
public class TimelineData
{
    public float duracaoTotal;
    public List<SerializableTrack> tracks = new List<SerializableTrack>();

    [Serializable]
    public class SerializableTrack
    {
        public string objectName;
        public string objectPath;
        public List<SerializableKeyframe> keyframes = new List<SerializableKeyframe>();
    }

    [Serializable]
    public class SerializableKeyframe
    {
        public float tempo;
        public string propriedade;
        public float valor;
    }
}

public class MinhaTimelineWindow : EditorWindow
{
    private float tempoAtual = 0f;
    private float duracaoTotal = 10f;
    private Vector2 scrollPosTimeline = Vector2.zero;
    private Vector2 scrollPosObjetos = Vector2.zero;
    private Vector2 scrollPosPropriedades = Vector2.zero;
    private bool isPlaying = false;
    private double lastTime;

    private const float TimelineHeight = 220f;
    private const float PixelsPerSecond = 100f;

    private List<AnimationTrack> tracks = new List<AnimationTrack>();
    private List<GameObject> objetosSelecionados = new List<GameObject>();

    private Dictionary<string, Vector3> valoresIniciaisPos = new Dictionary<string, Vector3>();
    private Dictionary<string, Vector3> valoresIniciaisRot = new Dictionary<string, Vector3>();
    private Dictionary<string, Vector3> valoresIniciaisScale = new Dictionary<string, Vector3>();

    [MenuItem("Window/Minha Timeline Customizada")]
    public static void ShowWindow()
    {
        GetWindow<MinhaTimelineWindow>("Minha Timeline").minSize = new Vector2(800, 600);
    }

    [MenuItem("Window/Minha Timeline Customizada _F2")]
    public static void ShowWindowF2()
    {
        ShowWindow();
    }

    void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        lastTime = EditorApplication.timeSinceStartup;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    void OnEditorUpdate()
    {
        if (isPlaying)
        {
            float deltaTime = (float)(EditorApplication.timeSinceStartup - lastTime);
            lastTime = EditorApplication.timeSinceStartup;
            tempoAtual += deltaTime;
            if (tempoAtual > duracaoTotal)
                tempoAtual = 0f; // loop

            AplicarAnimacoes();
            Repaint();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        
        GUILayout.Label("Timeline Customizada com Keyframes", EditorStyles.boldLabel);
		EditorGUILayout.Space();
		EditorGUILayout.Space();
		EditorGUILayout.Space();
		EditorGUILayout.Space();

        // ================== BARRA DE PROGRESSO GERAL ==================
        Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
        float progress = duracaoTotal > 0 ? tempoAtual / duracaoTotal : 0f;

        // Fundo da barra
        EditorGUI.DrawRect(progressRect, new Color(0.3f, 0.3f, 0.3f));

        // Barra preenchida
        Rect fillRect = progressRect;
        fillRect.width *= progress;
        EditorGUI.DrawRect(fillRect, new Color(0.8f, 0.2f, 0.2f)); // vermelho suave

        // Linha do playhead (fina)
        float playheadX = progressRect.x + progressRect.width * progress;
        EditorGUI.DrawRect(new Rect(playheadX - 1, progressRect.y, 2, progressRect.height), Color.red);

        // Texto do tempo no centro da barra
		string tempoTexto = string.Format("{0:F2}s / {1:F2}s", tempoAtual, duracaoTotal);
        GUIStyle centeredStyle = new GUIStyle(EditorStyles.boldLabel);
        centeredStyle.alignment = TextAnchor.MiddleCenter;
        centeredStyle.normal.textColor = Color.white;
        GUI.Label(progressRect, tempoTexto, centeredStyle);

		EditorGUILayout.Space();
		EditorGUILayout.Space();
		EditorGUILayout.Space();
		EditorGUILayout.Space();
		EditorGUILayout.Space();
        // ==============================================================

        // Duração Total
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Duração Total:", GUILayout.Width(100));
        float novaDuracao = EditorGUILayout.FloatField(duracaoTotal, GUILayout.Width(80));
        if (novaDuracao > 0 && novaDuracao != duracaoTotal)
        {
            duracaoTotal = novaDuracao;
            tempoAtual = Mathf.Clamp(tempoAtual, 0f, duracaoTotal);
        }
        GUILayout.Label("segundos");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Lista de Objetos
        GUILayout.Label("Objetos na Timeline:", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        float alturaItem = 24f;
        float alturaConteudo = (objetosSelecionados.Count * alturaItem) + 20f;
        float scrollViewHeight = 120f;

        scrollPosObjetos = EditorGUILayout.BeginScrollView(
            scrollPosObjetos,
            GUILayout.Width(position.width - 30),
            GUILayout.Height(scrollViewHeight)
        );

        EditorGUILayout.BeginVertical(GUILayout.Height(Mathf.Max(alturaConteudo, scrollViewHeight + 50f)));
        for (int i = 0; i < objetosSelecionados.Count; i++)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(alturaItem));
            objetosSelecionados[i] = (GameObject)EditorGUILayout.ObjectField(
                objetosSelecionados[i],
                typeof(GameObject),
                true,
                GUILayout.Height(20)
            );

            if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(20)))
            {
                RemoverObjeto(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
        GUILayout.Space(20);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("+ Adicionar Objeto", GUILayout.Height(30)))
            objetosSelecionados.Add(null);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        // Slider de Tempo (mantido como backup)
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Tempo:", GUILayout.Width(60));
        float novoTempo = EditorGUILayout.Slider(tempoAtual, 0f, duracaoTotal);
        if (novoTempo != tempoAtual)
        {
            tempoAtual = novoTempo;
            AplicarAnimacoes();
        }
        GUILayout.Label(tempoAtual.ToString("F2") + "s");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Propriedades Animáveis
        if (objetosSelecionados.Count > 0)
        {
            GUILayout.Label("Propriedades Animáveis:", EditorStyles.boldLabel);
            scrollPosPropriedades = EditorGUILayout.BeginScrollView(scrollPosPropriedades, GUILayout.Height(320));
            foreach (GameObject obj in objetosSelecionados)
            {
                if (obj == null) continue;
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(obj.name, EditorStyles.boldLabel);
                DesenharPropriedadesComKeyframes(obj);
                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.Space();

        // Timeline
        GUILayout.Label("Timeline", EditorStyles.boldLabel);
        float larguraConteudo = Mathf.Max(position.width, duracaoTotal * PixelsPerSecond);
        scrollPosTimeline = EditorGUILayout.BeginScrollView(scrollPosTimeline, GUILayout.Height(TimelineHeight));
        DesenharTimeline(larguraConteudo);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space();

        // Botões de Controle
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("<<", GUILayout.Width(60)))
        {
            tempoAtual = 0f;
            AplicarAnimacoes();
        }

        if (GUILayout.Button(isPlaying ? "■ Parar" : "▶ Play", GUILayout.Width(100)))
        {
            isPlaying = !isPlaying;
            if (isPlaying) lastTime = EditorApplication.timeSinceStartup;
        }

        if (GUILayout.Button("Stop", GUILayout.Width(70)))
        {
            isPlaying = false;
            tempoAtual = 0f;
            RestaurarValoresIniciais();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Salvar Animação")) SalvarAnimacao();
        if (GUILayout.Button("Carregar Animação")) CarregarAnimacao();
        if (GUILayout.Button("Limpar Tudo"))
        {
            if (EditorUtility.DisplayDialog("Confirmar", "Limpar tudo?", "Sim", "Não"))
            {
                tracks.Clear();
                objetosSelecionados.Clear();
                valoresIniciaisPos.Clear();
                valoresIniciaisRot.Clear();
                valoresIniciaisScale.Clear();
                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // === O RESTO DO CÓDIGO PERMANECE EXATAMENTE IGUAL ===
    // (RemoverObjeto, DesenharPropriedadesComKeyframes, etc.)

    void RemoverObjeto(int index)
    {
        GameObject obj = objetosSelecionados[index];
        objetosSelecionados.RemoveAt(index);
        tracks.RemoveAll(t => t.gameObject == obj);
        Repaint();
    }

    void DesenharPropriedadesComKeyframes(GameObject obj)
    {
        if (obj == null) return;
        Transform t = obj.transform;

        GUILayout.Label("Position", EditorStyles.miniBoldLabel);
        DesenharPropriedade(obj, "position.x", "X", t.position.x);
        DesenharPropriedade(obj, "position.y", "Y", t.position.y);
        DesenharPropriedade(obj, "position.z", "Z", t.position.z);

        GUILayout.Space(5);
        GUILayout.Label("Rotation", EditorStyles.miniBoldLabel);
        Vector3 rot = t.eulerAngles;
        DesenharPropriedade(obj, "rotation.x", "X", rot.x);
        DesenharPropriedade(obj, "rotation.y", "Y", rot.y);
        DesenharPropriedade(obj, "rotation.z", "Z", rot.z);

        GUILayout.Space(5);
        GUILayout.Label("Scale", EditorStyles.miniBoldLabel);
        DesenharPropriedade(obj, "scale.x", "X", t.localScale.x);
        DesenharPropriedade(obj, "scale.y", "Y", t.localScale.y);
        DesenharPropriedade(obj, "scale.z", "Z", t.localScale.z);
    }

    void DesenharPropriedade(GameObject obj, string propriedade, string label, float valorAtual)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(20));
        float novoValor = EditorGUILayout.FloatField(valorAtual, GUILayout.Width(80));
        if (novoValor != valorAtual)
            AplicarValorEmTempoReal(obj, propriedade, novoValor);

        GUILayout.FlexibleSpace();

        bool temKeyframe = TemKeyframeNoTempo(obj, propriedade, tempoAtual);
        Color oldColor = GUI.backgroundColor;
        if (temKeyframe) GUI.backgroundColor = Color.yellow;

        if (GUILayout.Button("KEY", GUILayout.Width(50), GUILayout.Height(20)))
        {
            if (!temKeyframe) SalvarValoresIniciais(obj);
            AdicionarKeyframe(obj, propriedade, novoValor);
        }

        GUI.backgroundColor = oldColor;
        EditorGUILayout.EndHorizontal();
    }

    void AplicarValorEmTempoReal(GameObject obj, string propriedade, float valor)
    {
        if (obj == null) return;
        Transform t = obj.transform;
        Undo.RecordObject(t, "Change Property Realtime");

        if (propriedade.StartsWith("position"))
        {
            Vector3 p = t.position;
            if (propriedade.EndsWith(".x")) p.x = valor;
            else if (propriedade.EndsWith(".y")) p.y = valor;
            else if (propriedade.EndsWith(".z")) p.z = valor;
            t.position = p;
        }
        else if (propriedade.StartsWith("rotation"))
        {
            Vector3 r = t.eulerAngles;
            if (propriedade.EndsWith(".x")) r.x = valor;
            else if (propriedade.EndsWith(".y")) r.y = valor;
            else if (propriedade.EndsWith(".z")) r.z = valor;
            t.eulerAngles = r;
        }
        else if (propriedade.StartsWith("scale"))
        {
            Vector3 s = t.localScale;
            if (propriedade.EndsWith(".x")) s.x = valor;
            else if (propriedade.EndsWith(".y")) s.y = valor;
            else if (propriedade.EndsWith(".z")) s.z = valor;
            t.localScale = s;
        }

        EditorUtility.SetDirty(obj);
    }

    void SalvarValoresIniciais(GameObject obj)
    {
        if (obj == null) return;
        string key = obj.GetInstanceID().ToString();
        Transform t = obj.transform;

        if (!valoresIniciaisPos.ContainsKey(key))
        {
            valoresIniciaisPos[key] = t.position;
            valoresIniciaisRot[key] = t.eulerAngles;
            valoresIniciaisScale[key] = t.localScale;
        }
    }

    void AdicionarKeyframe(GameObject obj, string propriedade, float valor)
    {
        if (obj == null) return;

        AnimationTrack track = tracks.Find(tr => tr.gameObject == obj);
        if (track == null)
        {
            track = new AnimationTrack(obj);
            tracks.Add(track);
        }

        track.keyframes.RemoveAll(kf => kf.propriedade == propriedade && Mathf.Abs(kf.tempo - tempoAtual) < 0.05f);

        AnimKeyframe novo = new AnimKeyframe(tempoAtual, propriedade, valor, obj);
        track.keyframes.Add(novo);
        track.keyframes.Sort((a, b) => a.tempo.CompareTo(b.tempo));

        Repaint();
    }

    bool TemKeyframeNoTempo(GameObject obj, string propriedade, float tempo)
    {
        AnimationTrack track = tracks.Find(tr => tr.gameObject == obj);
        if (track == null) return false;
        return track.keyframes.Exists(kf => kf.propriedade == propriedade && Mathf.Abs(kf.tempo - tempo) < 0.05f);
    }

    void DesenharTimeline(float larguraConteudo)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, TimelineHeight, GUILayout.Width(larguraConteudo));
        float pxPorSeg = rect.width / duracaoTotal;

        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

        for (int i = 0; i <= Mathf.CeilToInt(duracaoTotal); i++)
        {
            float x = rect.x + i * pxPorSeg;
            EditorGUI.DrawRect(new Rect(x, rect.y, 1, rect.height), new Color(0.4f, 0.4f, 0.4f));
            GUI.Label(new Rect(x - 10, rect.y - 18, 30, 16), i.ToString(), EditorStyles.miniLabel);
        }

        float y = rect.y + 10;
        foreach (AnimationTrack track in tracks)
        {
            if (track.gameObject == null) continue;

            GUI.Label(new Rect(rect.x + 5, y, 200, 16), track.gameObject.name, EditorStyles.boldLabel);
            y += 20;

            var grupos = new Dictionary<string, List<AnimKeyframe>>();
            foreach (var kf in track.keyframes)
            {
                if (!grupos.ContainsKey(kf.propriedade)) grupos[kf.propriedade] = new List<AnimKeyframe>();
                grupos[kf.propriedade].Add(kf);
            }

            foreach (var grupo in grupos)
            {
                GUI.Label(new Rect(rect.x + 15, y, 150, 14), grupo.Key.Replace(".", " "), EditorStyles.miniLabel);
                foreach (var kf in grupo.Value)
                {
                    float x = rect.x + kf.tempo * pxPorSeg;
                    Color cor = Mathf.Abs(kf.tempo - tempoAtual) < 0.05f ? Color.yellow : Color.cyan;
                    EditorGUI.DrawRect(new Rect(x - 3, y, 6, 14), cor);
                }
                y += 16;
            }
            y += 10;
        }

        if (y - rect.y > TimelineHeight)
        {
            GUILayoutUtility.GetRect(0, y - rect.y);
        }

        float playX = rect.x + tempoAtual * pxPorSeg;
        EditorGUI.DrawRect(new Rect(playX - 1, rect.y, 2, rect.height), Color.red);

        Event e = Event.current;
        if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
        {
            if (rect.Contains(e.mousePosition))
            {
                tempoAtual = Mathf.Clamp((e.mousePosition.x - rect.x) / pxPorSeg, 0f, duracaoTotal);
                AplicarAnimacoes();
                e.Use();
                Repaint();
            }
        }
    }

    void AplicarAnimacoes()
    {
        foreach (AnimationTrack track in tracks)
        {
            if (track.gameObject == null) continue;

            var grupos = new Dictionary<string, List<AnimKeyframe>>();
            foreach (var kf in track.keyframes)
            {
                if (!grupos.ContainsKey(kf.propriedade)) grupos[kf.propriedade] = new List<AnimKeyframe>();
                grupos[kf.propriedade].Add(kf);
            }

            foreach (var grupo in grupos)
            {
                var kfs = grupo.Value;
                AnimKeyframe prev = null;
                AnimKeyframe next = null;

                foreach (var kf in kfs)
                {
                    if (kf.tempo <= tempoAtual + 0.001f) prev = kf;
                    if (kf.tempo > tempoAtual && (next == null || kf.tempo < next.tempo)) next = kf;
                }

                float valor;
                if (prev != null && next != null)
                {
                    float t = (tempoAtual - prev.tempo) / (next.tempo - prev.tempo);
                    valor = Mathf.Lerp(prev.valor, next.valor, t);
                }
                else if (prev != null)
                {
                    valor = prev.valor;
                }
                else continue;

                AplicarValor(track.gameObject, grupo.Key, valor);
            }
        }
    }

    void AplicarValor(GameObject obj, string propriedade, float valor)
    {
        if (obj == null) return;
        Transform t = obj.transform;
        Undo.RecordObject(t, "Animate Property");

        if (propriedade.StartsWith("position"))
        {
            Vector3 p = t.position;
            if (propriedade.EndsWith(".x")) p.x = valor;
            else if (propriedade.EndsWith(".y")) p.y = valor;
            else if (propriedade.EndsWith(".z")) p.z = valor;
            t.position = p;
        }
        else if (propriedade.StartsWith("rotation"))
        {
            Vector3 r = t.eulerAngles;
            if (propriedade.EndsWith(".x")) r.x = valor;
            else if (propriedade.EndsWith(".y")) r.y = valor;
            else if (propriedade.EndsWith(".z")) r.z = valor;
            t.eulerAngles = r;
        }
        else if (propriedade.StartsWith("scale"))
        {
            Vector3 s = t.localScale;
            if (propriedade.EndsWith(".x")) s.x = valor;
            else if (propriedade.EndsWith(".y")) s.y = valor;
            else if (propriedade.EndsWith(".z")) s.z = valor;
            t.localScale = s;
        }

        EditorUtility.SetDirty(obj);
    }

    void RestaurarValoresIniciais()
    {
        foreach (AnimationTrack track in tracks)
        {
            if (track.gameObject == null) continue;
            string key = track.gameObject.GetInstanceID().ToString();

            if (valoresIniciaisPos.ContainsKey(key))
            {
                Undo.RecordObject(track.gameObject.transform, "Restore Initial Values");
                track.gameObject.transform.position = valoresIniciaisPos[key];
                track.gameObject.transform.eulerAngles = valoresIniciaisRot[key];
                track.gameObject.transform.localScale = valoresIniciaisScale[key];
                EditorUtility.SetDirty(track.gameObject);
            }
        }
        Repaint();
    }

    void SalvarAnimacao()
    {
        string path = EditorUtility.SaveFilePanel("Salvar Animação", Application.dataPath, "animacao", "json");
        if (string.IsNullOrEmpty(path)) return;

        TimelineData data = new TimelineData();
        data.duracaoTotal = duracaoTotal;

        foreach (AnimationTrack track in tracks)
        {
            if (track.gameObject == null) continue;

            var sTrack = new TimelineData.SerializableTrack();
            sTrack.objectName = track.gameObject.name;
            sTrack.objectPath = GetGameObjectPath(track.gameObject);

            foreach (var kf in track.keyframes)
            {
                var sKf = new TimelineData.SerializableKeyframe();
                sKf.tempo = kf.tempo;
                sKf.propriedade = kf.propriedade;
                sKf.valor = kf.valor;
                sTrack.keyframes.Add(sKf);
            }
            data.tracks.Add(sTrack);
        }

        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            Debug.Log("Animação salva em: " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao salvar: " + e.Message);
        }
    }

    void CarregarAnimacao()
    {
        string path = EditorUtility.OpenFilePanel("Carregar Animação", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            string json = File.ReadAllText(path);
            TimelineData data = JsonUtility.FromJson<TimelineData>(json);

            duracaoTotal = data.duracaoTotal;
            tracks.Clear();
            objetosSelecionados.Clear();
            valoresIniciaisPos.Clear();
            valoresIniciaisRot.Clear();
            valoresIniciaisScale.Clear();

            foreach (var sTrack in data.tracks)
            {
                GameObject obj = FindGameObjectByPath(sTrack.objectPath);

                if (obj == null)
                {
                    GameObject[] all = FindObjectsOfType<GameObject>();
                    foreach (var go in all)
                    {
                        if (go.name == sTrack.objectName && go.scene.isLoaded)
                        {
                            obj = go;
                            break;
                        }
                    }
                }

                if (obj == null)
                {
                    Debug.LogWarning("Objeto não encontrado: " + sTrack.objectName + " (caminho: " + sTrack.objectPath + ")");
                    continue;
                }

                if (objetosSelecionados.Contains(obj)) continue;

                objetosSelecionados.Add(obj);
                SalvarValoresIniciais(obj);

                AnimationTrack track = new AnimationTrack(obj);
                foreach (var sKf in sTrack.keyframes)
                {
                    track.keyframes.Add(new AnimKeyframe(sKf.tempo, sKf.propriedade, sKf.valor, obj));
                }
                track.keyframes.Sort((a, b) => a.tempo.CompareTo(b.tempo));
                tracks.Add(track);
            }

            Repaint();
            Debug.Log("Animação carregada com sucesso!");
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao carregar: " + e.Message);
        }
    }

    string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    GameObject FindGameObjectByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        string[] names = path.Split('/');
        if (names.Length == 0) return null;

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == names[0])
            {
                Transform current = root.transform;
                for (int i = 1; i < names.Length; i++)
                {
                    current = current.Find(names[i]);
                    if (current == null) return null;
                }
                return current.gameObject;
            }
        }
        return null;
    }
}