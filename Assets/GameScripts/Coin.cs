using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("Hover Settings")]
    [SerializeField] private float hoverHeight = 0.3f;      // Altura da flutuação
    [SerializeField] private float hoverSpeed = 1.5f;       // Velocidade da flutuação
    [SerializeField] private float rotationSpeed = 90f;     // Velocidade de rotação (graus por segundo)

    [Header("Collection Settings")]
    [SerializeField] private GameObject particlePrefab;      // Prefab do sistema de partículas (deve ter um ParticleSystem)
    [SerializeField] private float particleDuration = 2f;    // Tempo que as partículas ficam ativas antes de serem destruídas

    [Header("Audio (opcional)")]
    [SerializeField] private AudioClip collectSound;         // Som de coleta (opcional)
    [SerializeField] private float soundVolume = 1f;

    // Referências internas
    private Vector3 startPosition;
    private float randomOffset; // Para dar variação entre moedas

    private void Start()
    {
        // Guarda a posição inicial
        startPosition = transform.position;
        // Gera um offset aleatório para cada moeda (movimento dessincronizado)
        randomOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        // --- Flutuação (hover) ---
        float newY = startPosition.y + Mathf.Sin((Time.time + randomOffset) * hoverSpeed) * hoverHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // --- Rotação contínua ---
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    // Detecção de colisão com o jogador (assumindo que o jogador tem um Collider com trigger ou não)
    private void OnTriggerEnter(Collider other)
    {
        // Verifica se o objeto que colidiu é o jogador
        // Você pode usar tag "Player" ou uma camada específica
        if (other.CompareTag("Player"))
        {
            Collect();
        }
    }

    // Também funciona com colisão normal (não trigger)
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Collect();
        }
    }

    private void Collect()
    {
        // --- Toca o som de coleta (se houver) ---
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position, soundVolume);
        }

        // --- Spawna as partículas ---
        if (particlePrefab != null)
        {
            GameObject particles = Instantiate(particlePrefab, transform.position, Quaternion.identity);
            // Destroi as partículas após um tempo (para não poluir a cena)
            Destroy(particles, particleDuration);
        }
        else
        {
            Debug.LogWarning("Particle prefab não atribuído na moeda " + gameObject.name);
        }

        // --- Desativa a moeda ---
        // Opção 1: Destruir imediatamente
        // Destroy(gameObject);

        // Opção 2: Desativar e depois destruir (mais suave para animações)
        gameObject.SetActive(false);
        Destroy(gameObject, 0.5f); // Destroi após meio segundo (dá tempo de tocar som/soltar partículas)
    }
}