using UnityEngine;
using System.Collections.Generic;

public class TimelinePlayer : MonoBehaviour
{
	[Header("JSON")]
	public TextAsset timelineJson;

	[Header("Config")]
	public bool loop = false;
	[Tooltip("Número de vezes para repetir (0 = infinito, ignora o 'loop' se > 0)")]
	public int repeatCount = 0;

	[Header("Reprodução")]
	public bool playOnStart = false;                    // ← NOVA OPÇÃO
	[Tooltip("Marque para dar Play, desmarque para parar (funciona também em Edit Mode)")]
	[SerializeField] private bool playManually = false;

	private TimelineData data;
	private float tempo = 0f;
	private bool playing = false;
	private int currentLoop = 0;

	private Dictionary<string, GameObject> cachedObjects = new Dictionary<string, GameObject>();

	void Start()
	{
		if (timelineJson != null)
		{
			LoadFromJson(timelineJson.text);
		}

		// Nova funcionalidade: reproduzir automaticamente ao iniciar
		if (playOnStart && data != null)
		{
			Play();
		}
	}

	void Update()
	{
		// Detecta mudança no toggle manual do Inspector (funciona em Edit e Play Mode)
		if (playManually && !playing && data != null)
		{
			Play();
		}
		else if (!playManually && playing)
		{
			Stop();
		}

		if (!playing || data == null) return;

		tempo += Time.deltaTime;

		if (tempo > data.duracaoTotal)
		{
			currentLoop++;

			bool deveRepetir = false;
			if (repeatCount > 0)
			{
				if (currentLoop < repeatCount) deveRepetir = true;
			}
			else if (loop)
			{
				deveRepetir = true;
			}

			if (deveRepetir)
			{
				tempo = 0f;
			}
			else
			{
				playing = false;
				tempo = data.duracaoTotal;
				playManually = false;
				Debug.Log("[TimelinePlayer] Animação terminou após " + currentLoop + " execução(ões).");
			}
		}

		AplicarTimeline();
	}

	// ============================================================
	// Botões no Inspector
	// ============================================================
	[ContextMenu("Play Timeline")]
	public void Play()
	{
		if (data == null)
		{
			Debug.LogError("[TimelinePlayer] Nenhuma timeline carregada!");
			playManually = false;
			return;
		}

		tempo = 0f;
		playing = true;
		playManually = true;
		Debug.Log("[TimelinePlayer] Iniciando animação...");
	}

	[ContextMenu("Stop Timeline")]
	public void Stop()
	{
		playing = false;
		tempo = 0f;
		playManually = false;
		Debug.Log("[TimelinePlayer] Animação parada e resetada.");
	}

	// ============================================================
	public void LoadFromJson(string json)
	{
		data = JsonUtility.FromJson<TimelineData>(json);

		if (data == null)
		{
			Debug.LogError("[TimelinePlayer] Falha ao ler o JSON da timeline.");
			return;
		}

		cachedObjects.Clear();

		foreach (TimelineData.SerializableTrack track in data.tracks)
		{
			GameObject obj = FindObjectByPath(track.objectPath);
			if (obj != null)
			{
				cachedObjects[track.objectPath] = obj;
			}
			else
			{
				Debug.LogWarning("[TimelinePlayer] Objeto não encontrado: " + track.objectPath);
			}
		}

		Debug.Log("[TimelinePlayer] Timeline carregada: " + data.tracks.Count + " tracks, duração " + data.duracaoTotal + "s");
	}

	void AplicarTimeline()
	{
		foreach (TimelineData.SerializableTrack track in data.tracks)
		{
			GameObject obj = null;
			if (!cachedObjects.TryGetValue(track.objectPath, out obj) || obj == null)
				continue;

			Transform trans = obj.transform;

			// Agrupa keyframes por propriedade
			Dictionary<string, List<TimelineData.SerializableKeyframe>> props =
				new Dictionary<string, List<TimelineData.SerializableKeyframe>>();

			foreach (TimelineData.SerializableKeyframe kf in track.keyframes)
			{
				if (!props.ContainsKey(kf.propriedade))
				{
					props[kf.propriedade] = new List<TimelineData.SerializableKeyframe>();
				}
				props[kf.propriedade].Add(kf);
			}

			// Processa cada propriedade
			foreach (var pair in props)
			{
				string prop = pair.Key;
				List<TimelineData.SerializableKeyframe> kfs = pair.Value;

				TimelineData.SerializableKeyframe prev = null;
				TimelineData.SerializableKeyframe next = null;

				foreach (TimelineData.SerializableKeyframe kf in kfs)
				{
					if (kf.tempo <= tempo + 0.001f)
					{
						prev = kf;
					}
					else if (next == null || kf.tempo < next.tempo)
					{
						next = kf;
					}
				}

				float valor;
				if (prev != null && next != null)
				{
					float t = (tempo - prev.tempo) / (next.tempo - prev.tempo);
					valor = Mathf.Lerp(prev.valor, next.valor, t);
				}
				else if (prev != null)
				{
					valor = prev.valor;
				}
				else
				{
					continue;
				}

				AplicarValor(trans, prop, valor);
			}
		}
	}

	void AplicarValor(Transform trans, string propriedade, float valor)
	{
		Vector3 p = trans.position;
		Vector3 r = trans.eulerAngles;
		Vector3 s = trans.localScale;

		if (propriedade == "position.x" || propriedade == "positionX") p.x = valor;
		else if (propriedade == "position.y" || propriedade == "positionY") p.y = valor;
		else if (propriedade == "position.z" || propriedade == "positionZ") p.z = valor;
		else if (propriedade == "rotation.x" || propriedade == "rotationX") r.x = valor;
		else if (propriedade == "rotation.y" || propriedade == "rotationY") r.y = valor;
		else if (propriedade == "rotation.z" || propriedade == "rotationZ") r.z = valor;
		else if (propriedade == "scale.x" || propriedade == "scaleX") s.x = valor;
		else if (propriedade == "scale.y" || propriedade == "scaleY") s.y = valor;
		else if (propriedade == "scale.z" || propriedade == "scaleZ") s.z = valor;

		trans.position = p;
		trans.eulerAngles = r;
		trans.localScale = s;
	}

	GameObject FindObjectByPath(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;

		string[] parts = path.Split('/');
		Transform current = null;

		for (int i = 0; i < parts.Length; i++)
		{
			string name = parts[i];
			if (string.IsNullOrEmpty(name)) continue;

			GameObject go;
			if (i == 0)
				go = GameObject.Find(name);
			else
			{
				if (current == null) return null;
				Transform child = current.Find(name);
				if (child == null) return null;
				go = child.gameObject;
			}

			if (go == null) return null;
			current = go.transform;
		}

		if (current == null)
			return null;

		return current.gameObject;
	}
}