/// <summary>
/// Calcula el score de una sala completada.
/// Clase estática — sin estado, sin dependencias, fácil de testear.
///
/// FÓRMULA:
/// Score = BaseTime - timeElapsed + (livesRemaining * LivesBonus)
/// Si el tiempo supera el BaseTime, el score de tiempo es 0 (nunca negativo).
///
/// La diferencia entre sala normal y sala de boss es solo el BaseTime:
/// el boss naturalmente toma más tiempo, así que el tiempo base
/// es mayor para que el score no sea injustamente bajo.
/// </summary>
public static class ScoreCalculator
{
    // Tiempo base en segundos para sala normal
    // Si el jugador termina antes, el sobrante suma al score
    private const float NormalBaseTime = 120f;

    // El boss toma más tiempo — base más alto para compensar
    private const float BossBaseTime = 300f;

    // Bonus por cada vida restante al completar la sala
    private const int LivesBonus = 500;

    // Bonus base por completar la sala
    private const int CompletionBonus = 1000;

    public static int Calculate(float timeElapsed, int livesRemaining, bool isBossRoom)
    {
        float baseTime = isBossRoom ? BossBaseTime : NormalBaseTime;

        // Tiempo sobrante convertido a puntos — nunca negativo
        float timeRemaining = UnityEngine.Mathf.Max(0f, baseTime - timeElapsed);
        int timeScore = (int)timeRemaining;

        int livesScore = livesRemaining * LivesBonus;

        return CompletionBonus + timeScore + livesScore;
    }
}