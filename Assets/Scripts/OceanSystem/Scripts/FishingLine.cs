using UnityEngine;

/// <summary>
/// Simulates a fishing line with physically-plausible slack between two points
/// using a catenary curve (the shape a hanging chain makes under gravity).
///
/// The line is rendered via a LineRenderer and responds dynamically to the
/// distance between the rod tip and the lure/hook:
///
///   • When the lure is closer than the resting length → line sags with
///     configurable slack, increasing droop as it gets closer.
///   • When the lure is at or beyond the resting length → line pulls taut,
///     slack reduces to zero. Beyond max stretch it clamps to the line length.
///   • Slack transitions smoothly with configurable tension response speed.
///   • An optional bob/wave animation simulates water surface movement on
///     the line when slack is present.
///
/// ── Requirements ─────────────────────────────────────────────────────────────
///   • A LineRenderer component on this GameObject (auto-added if missing).
///   • Two Transform points: rodTip and lure.
///
/// ── Setup ────────────────────────────────────────────────────────────────────
///   1. Add this component to any GameObject (e.g. the fishing rod).
///   2. Assign rodTip (the end of the rod) and lure (hook/bobber Transform).
///   3. Set lineRestLength to the natural cast distance.
///   4. Tune slackAmount, segmentCount, and tensionSpeed to taste.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class FishingLine : MonoBehaviour
{
	// ─────────────────────────────────────────────────────────────────────────
	// Inspector – End Points
	// ─────────────────────────────────────────────────────────────────────────
	[Header("End Points")] [Tooltip("The tip of the fishing rod — where the line begins.")]
	public Transform rodTip;

	[Tooltip("The lure, hook, or bobber — where the line ends.")]
	public Transform lure;

	[Header("Health")] [SerializeField] private Gradient m_healthGradient;

	// ─────────────────────────────────────────────────────────────────────────
	// Inspector – Line Shape
	// ─────────────────────────────────────────────────────────────────────────
	[Header("Line Shape")]
	[Tooltip("The natural length of the line when cast. When the lure is closer " +
	         "than this distance the line sags; at this distance it is taut.")]
	[Range(0.5f, 50f)]
	public float lineRestLength = 10f;

	[Tooltip("Maximum slack droop at rest (metres below the chord midpoint). " +
	         "This is the sag when the lure is at its closest configurable point.")]
	[Range(0f, 10f)]
	public float maxSlackDrop = 1.5f;

	[Tooltip("Closest distance (as a fraction of lineRestLength) at which the line " +
	         "is considered fully slack. Below this fraction, slack no longer increases.")]
	[Range(0f, 0.9f)]
	public float fullSlackFraction = 0.3f;

	[Tooltip("Number of segments used to draw the catenary curve. " +
	         "Higher = smoother curve but more vertices.")]
	[Range(4, 64)]
	public int segmentCount = 24;

	[Tooltip("How quickly the slack value transitions as the lure moves. " +
	         "Higher = snappier tension response.")]
	[Range(0.5f, 20f)]
	public float tensionSpeed = 6f;

	// ─────────────────────────────────────────────────────────────────────────
	// Inspector – Line Renderer appearance
	// ─────────────────────────────────────────────────────────────────────────
	[Header("Line Renderer")] [Tooltip("Width of the line at the rod tip.")] [Range(0.001f, 0.05f)]
	public float lineWidthStart = 0.008f;

	[Tooltip("Width of the line at the lure.")] [Range(0.001f, 0.05f)]
	public float lineWidthEnd = 0.004f;

	[Tooltip("Material to apply to the LineRenderer. If null, the existing " +
	         "material on the LineRenderer is kept.")]
	public Material lineMaterial;

	// ─────────────────────────────────────────────────────────────────────────
	// Inspector – Water / Bob animation
	// ─────────────────────────────────────────────────────────────────────────
	[Header("Water Bob (optional)")]
	[Tooltip("Apply a subtle sine-wave undulation to the slack portion of the " +
	         "line, simulating water surface movement or wind.")]
	public bool enableBobAnimation = true;

	[Tooltip("Maximum vertical amplitude of the bob animation (metres).")] [Range(0f, 0.5f)]
	public float bobAmplitude = 0.06f;

	[Tooltip("Frequency of the bob animation (cycles per second).")] [Range(0.1f, 5f)]
	public float bobFrequency = 0.8f;

	[Tooltip("Bob animation only applies to the slack portions of the line, " +
	         "fading out as the line approaches full tension.")]
	public bool bobFadesWithTension = true;

	// ─────────────────────────────────────────────────────────────────────────
	// Inspector – Manual Override
	// ─────────────────────────────────────────────────────────────────────────
	[Header("Manual Override")]
	[Tooltip("0 = no effect (physics-driven slack applies normally).\n" +
	         "1 = line forced to maximum tautness regardless of lure distance.\n" +
	         "Values between 0 and 1 blend between physics slack and fully taut, " +
	         "allowing gradual manual tightening (e.g. reeling in, animation curves).")]
	[Range(0f, 1f)]
	public float tautnessOverride = 0f;

	// ─────────────────────────────────────────────────────────────────────────
	// Inspector – Debug
	// ─────────────────────────────────────────────────────────────────────────
	[Header("Debug")] [Tooltip("Draw the chord and sag midpoint in the Scene view.")]
	public bool debugDraw = true;

	// ─────────────────────────────────────────────────────────────────────────
	// Public read-only state
	// ─────────────────────────────────────────────────────────────────────────
	/// <summary>Current smoothed slack value in [0, 1]. 0 = fully taut, 1 = fully slack.</summary>
	public float CurrentSlack { get; private set; }

	/// <summary>Current actual distance between rod tip and lure.</summary>
	public float CurrentDistance { get; private set; }

	/// <summary>True when the line is fully taut (lure at or beyond rest length).</summary>
	public bool IsTaut { get; private set; }

	/// <summary>
	/// The slack value actually used for rendering, after applying
	/// <see cref="tautnessOverride"/>. This is what the catenary and bob
	/// animation read — use this rather than <see cref="CurrentSlack"/> when
	/// you need to know how the line looks right now.
	/// 0 = fully taut, 1 = fully slack.
	/// </summary>
	public float EffectiveSlack { get; private set; }

	// ─────────────────────────────────────────────────────────────────────────
	// Private
	// ─────────────────────────────────────────────────────────────────────────
	LineRenderer _lr;
	Vector3[] _points; // reused each frame — sized to segmentCount + 1
	float _targetSlack; // 0–1, drives CurrentSlack via lerp
	private Gradient m_tempGradient = new Gradient();
	private GradientColorKey[] m_tempColorKeys = new  GradientColorKey[2];

	// ─────────────────────────────────────────────────────────────────────────
	// Unity lifecycle
	// ─────────────────────────────────────────────────────────────────────────
	void Awake()
	{
		m_tempGradient = new Gradient();
		var colors = new GradientColorKey[2];
		colors[0] = new GradientColorKey(Color.white, 0.0f);
		colors[1] = new GradientColorKey(Color.white, 1.0f);

		var alphas = new GradientAlphaKey[2];
		alphas[0] = new GradientAlphaKey(1f, 0.0f);
		alphas[1] = new GradientAlphaKey(1f, 1.0f);

		m_tempColorKeys[0].time = 0f;
		m_tempColorKeys[1].time = 1f;
		
		m_tempGradient.SetKeys(colors, alphas);

		_lr = GetComponent<LineRenderer>();
		ConfigureLineRenderer();
		_points = new Vector3[segmentCount + 1];
	}

	private void OnEnable()
	{
		Application.onBeforeRender += UpdateLine;
	}

	private void OnDisable()
	{
		Application.onBeforeRender -= UpdateLine;
	}

	void OnValidate()
	{
		// Rebuild point buffer if segment count changed in the Inspector
		if (_points == null || _points.Length != segmentCount + 1)
			_points = new Vector3[segmentCount + 1];

		if (_lr != null)
			ConfigureLineRenderer();
	}

	private void UpdateLine()
	{
		if (rodTip == null || lure == null) return;

		UpdateSlack();
		BuildCatenaryPoints();
		ApplyBobAnimation();
		_lr.positionCount = _points.Length;
		_lr.SetPositions(_points);
	}

	public void UpdateHealthColor(float healthPercent)
	{
		Color lerpedColor = m_healthGradient.Evaluate(healthPercent);
		m_tempColorKeys[0].color = lerpedColor;
		m_tempColorKeys[1].color = lerpedColor;
		m_tempGradient.SetColorKeys(m_tempColorKeys);
		_lr.colorGradient = m_tempGradient;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Line Renderer setup
	// ─────────────────────────────────────────────────────────────────────────
	void ConfigureLineRenderer()
	{
		_lr.useWorldSpace = true;
		_lr.textureMode = LineTextureMode.Stretch;
		_lr.numCornerVertices = 4;
		_lr.numCapVertices = 2;
		_lr.widthCurve = AnimationCurve.Linear(0f, lineWidthStart, 1f, lineWidthEnd);
		_lr.positionCount = segmentCount + 1;

		if (lineMaterial != null)
			_lr.sharedMaterial = lineMaterial;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Slack calculation
	// ─────────────────────────────────────────────────────────────────────────
	void UpdateSlack()
	{
		Vector3 a = rodTip.position;
		Vector3 b = lure.position;
		CurrentDistance = Vector3.Distance(a, b);

		float fullSlackDist = lineRestLength * fullSlackFraction;

		if (CurrentDistance >= lineRestLength)
		{
			// At or beyond rest length → taut
			_targetSlack = 0f;
			IsTaut = true;
		}
		else if (CurrentDistance <= fullSlackDist)
		{
			// Closer than the full-slack threshold → maximum droop
			_targetSlack = 1f;
			IsTaut = false;
		}
		else
		{
			// In between: linearly interpolate slack based on how close the lure is
			// t = 0 at lineRestLength (taut), t = 1 at fullSlackDist (full sag)
			float t = 1f - (CurrentDistance - fullSlackDist) / (lineRestLength - fullSlackDist);
			_targetSlack = Mathf.Clamp01(t);
			IsTaut = false;
		}

		// Smooth transition — snappier when tightening, gentler when slackening
		float speed = _targetSlack < CurrentSlack
			? tensionSpeed * 1.5f // tightening is faster (line snaps taut)
			: tensionSpeed;
		CurrentSlack = Mathf.MoveTowards(CurrentSlack, _targetSlack, speed * Time.deltaTime);

		// Apply the manual tautness override.
		// tautnessOverride = 0 → EffectiveSlack = CurrentSlack  (no change)
		// tautnessOverride = 1 → EffectiveSlack = 0             (fully taut)
		// Intermediate values linearly reduce the slack toward zero.
		EffectiveSlack = CurrentSlack * (1f - tautnessOverride);

		// IsTaut reflects the visual result, not just physics distance
		IsTaut = EffectiveSlack < 0.001f;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Catenary curve construction
	// ─────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Fills <see cref="_points"/> with world-space positions along a catenary
	/// curve between rodTip and lure. When <see cref="CurrentSlack"/> is 0 the
	/// curve degenerates to a straight line.
	/// </summary>
	void BuildCatenaryPoints()
	{
		Vector3 a = rodTip.position;
		Vector3 b = lure.position;
		float sag = maxSlackDrop * EffectiveSlack;

		for (int i = 0; i <= segmentCount; i++)
		{
			float t = i / (float)segmentCount;
			_points[i] = CatenaryPoint(a, b, t, sag);
		}
	}

	/// <summary>
	/// Returns a point along a catenary between two world-space endpoints.
	///
	/// The catenary is approximated by a parabola (accurate for shallow sag
	/// angles, which covers all practical fishing line geometries). The sag
	/// is applied perpendicular to the chord and then in world-space down (−Y)
	/// so the line always hangs naturally under gravity regardless of rod angle.
	/// </summary>
	static Vector3 CatenaryPoint(Vector3 a, Vector3 b, float t, float sag)
	{
		// Linear interpolation along the chord
		Vector3 chord = Vector3.Lerp(a, b, t);

		if (sag < 0.0001f)
			return chord;

		// Parabolic sag offset: maximum at t = 0.5, zero at both ends
		// Shape: 4 * t * (1 - t) gives a clean parabola peak of 1.0 at t=0.5
		float sagFactor = 4f * t * (1f - t);

		// The sag direction is always world-down so the line hangs under gravity.
		// We scale it by (1 - |chord angle|) so a nearly vertical line doesn't
		// droop sideways — a fishing line pointing straight down has minimal sag.
		Vector3 chordDir = (b - a).normalized;
		float verticalness = Mathf.Abs(Vector3.Dot(chordDir, Vector3.down));
		// verticalness = 0 when horizontal (full sag), 1 when vertical (no sag)
		float effectiveSag = sag * (1f - verticalness);

		return chord + Vector3.down * (sagFactor * effectiveSag);
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Bob animation
	// ─────────────────────────────────────────────────────────────────────────
	void ApplyBobAnimation()
	{
		if (!enableBobAnimation || bobAmplitude < 0.0001f) return;

		float slackMask = bobFadesWithTension ? CurrentSlack : 1f;
		if (slackMask < 0.0001f) return;

		float t = Time.time;
		int n = _points.Length;

		for (int i = 1; i < n - 1; i++) // leave endpoints fixed
		{
			// Position along the line (0–1)
			float lineT = i / (float)(n - 1);

			// Bob fades at both ends so the endpoints stay fixed
			float endFade = Mathf.Sin(lineT * Mathf.PI);

			// Phase offset so the wave travels along the line
			float phase = lineT * Mathf.PI * 2f - t * bobFrequency * Mathf.PI * 2f;
			float wave = Mathf.Sin(phase) * bobAmplitude * endFade * slackMask;

			_points[i] += Vector3.up * wave;
		}
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Public API
	// ─────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Instantly snaps the current slack to the target slack without smoothing.
	/// Useful when teleporting the lure.
	/// </summary>
	public void SnapSlack()
	{
		UpdateSlack();
		CurrentSlack = _targetSlack;
		EffectiveSlack = CurrentSlack * (1f - tautnessOverride);
		IsTaut = EffectiveSlack < 0.001f;
	}

	/// <summary>
	/// Returns the world-space position along the line at normalised parameter t [0,1].
	/// t = 0 is the rod tip, t = 1 is the lure. Useful for attaching effects
	/// (e.g. droplets, guides) at specific points along the line.
	/// </summary>
	public Vector3 GetPositionAlongLine(float t)
	{
		if (rodTip == null || lure == null) return Vector3.zero;
		t = Mathf.Clamp01(t);
		return CatenaryPoint(rodTip.position, lure.position, t, maxSlackDrop * EffectiveSlack);
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Gizmos
	// ─────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
	void OnDrawGizmosSelected()
	{
		if (!debugDraw || rodTip == null || lure == null) return;

		Vector3 a = rodTip.position;
		Vector3 b = lure.position;
		Vector3 mid = (a + b) * 0.5f;
		float dist = Vector3.Distance(a, b);

		// Chord line
		Gizmos.color = Color.white;
		Gizmos.DrawLine(a, b);

		// Sag midpoint
		float slack = Application.isPlaying ? EffectiveSlack : 0.5f * (1f - tautnessOverride);
		float sag = maxSlackDrop * slack;
		Vector3 sagMid = CatenaryPoint(a, b, 0.5f, sag);
		Gizmos.color = Color.cyan;
		Gizmos.DrawSphere(sagMid, 0.04f);
		Gizmos.DrawLine(mid, sagMid);

		// Rest length arc indicator
		Gizmos.color = dist >= lineRestLength
			? new Color(1f, 0.2f, 0.2f, 0.4f) // taut – red
			: new Color(0.2f, 1f, 0.4f, 0.4f); // slack – green
		Gizmos.DrawWireSphere(a, lineRestLength);

		// Full-slack radius
		Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
		Gizmos.DrawWireSphere(a, lineRestLength * fullSlackFraction);

		// Status label
#if UNITY_EDITOR
		string overrideTag = tautnessOverride > 0.001f ? $"  override={tautnessOverride:F2}" : "";
		UnityEditor.Handles.Label(sagMid + Vector3.up * 0.15f,
			$"effective={slack:F2}  physics={(Application.isPlaying ? CurrentSlack : 0f):F2}  dist={dist:F1}m  {(IsTaut ? "TAUT" : "slack")}{overrideTag}");
#endif
	}
#endif
}