using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class InimigoIA : MonoBehaviour
{
    public enum Estado { Patrulha, Perseguicao }

    [Header("Estado Atual (apenas visualização)")]
    [SerializeField] private Estado estadoAtual = Estado.Patrulha;

    [Header("Pontos de Patrulha")]
    [Tooltip("Arraste GameObjects vazios que servirão como pontos de patrulha")]
    public List<Transform> pontosDePatrulha = new List<Transform>();

    [Header("Configurações")]
    [Tooltip("Distância mínima para considerar que chegou ao ponto")]
    public float distanciaMinimaPonto = 0.5f;

    [Tooltip("Tempo parado em cada ponto de patrulha")]
    public float tempoDeEsperaNoPonto = 1f;

    [Tooltip("Velocidade de patrulha")]
    public float velocidadePatrulha = 2f;

    [Tooltip("Velocidade de perseguição")]
    public float velocidadePerseguicao = 5f;

    [Tooltip("Tag do alvo (player)")]
    public string tagDoAlvo = "Player";

    private NavMeshAgent agent;
    private Transform alvo;
    private int indexPontoAtual = 0;
    private float tempoEsperando = 0f;
    private bool esperando = false;

    // Referência do alvo guardada pelo trigger
    private Transform alvoNoTrigger;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // Tenta encontrar o player pela tag
        GameObject playerObj = GameObject.FindGameObjectWithTag(tagDoAlvo);
        if (playerObj != null)
            alvo = playerObj.transform;
        else
            Debug.LogWarning($"[{name}] Nenhum objeto com a tag '{tagDoAlvo}' encontrado!");

        // Inicia em patrulha
        MudarEstado(Estado.Patrulha);
    }

    void Update()
    {
        switch (estadoAtual)
        {
            case Estado.Patrulha:
                AtualizarPatrulha();
                break;
            case Estado.Perseguicao:
                AtualizarPerseguicao();
                break;
        }
    }

    // ==================== PATRULHA ====================
    private void AtualizarPatrulha()
    {
        if (pontosDePatrulha == null || pontosDePatrulha.Count == 0)
            return;

        // Se estiver esperando no ponto
        if (esperando)
        {
            tempoEsperando -= Time.deltaTime;
            if (tempoEsperando <= 0f)
            {
                esperando = false;
                IrParaProximoPonto();
            }
            return;
        }

        // Verifica se chegou ao ponto atual
        if (!agent.pathPending && agent.remainingDistance <= distanciaMinimaPonto)
        {
            esperando = true;
            tempoEsperando = tempoDeEsperaNoPonto;
        }
    }

    private void IrParaProximoPonto()
    {
        if (pontosDePatrulha.Count == 0) return;

        // Avança o índice (loop circular)
        indexPontoAtual = (indexPontoAtual + 1) % pontosDePatrulha.Count;
        agent.SetDestination(pontosDePatrulha[indexPontoAtual].position);
    }

    // ==================== PERSEGUIÇÃO ====================
    private void AtualizarPerseguicao()
    {
        if (alvo == null)
        {
            MudarEstado(Estado.Patrulha);
            return;
        }

        // Atualiza destino constantemente para perseguir o player em movimento
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(alvo.position);
        }
    }

    // ==================== TROCA DE ESTADO ====================
    private void MudarEstado(Estado novoEstado)
    {
        if (estadoAtual == novoEstado) return;

        estadoAtual = novoEstado;
        esperando = false;

        if (novoEstado == Estado.Patrulha)
        {
            agent.speed = velocidadePatrulha;
            if (pontosDePatrulha.Count > 0)
            {
                // Vai para o ponto mais próximo ao invés de continuar do índice atual
                indexPontoAtual = EncontrarPontoMaisProximo();
                agent.SetDestination(pontosDePatrulha[indexPontoAtual].position);
            }
        }
        else if (novoEstado == Estado.Perseguicao)
        {
            agent.speed = velocidadePerseguicao;
        }
    }

    private int EncontrarPontoMaisProximo()
    {
        int indiceMaisProximo = 0;
        float menorDistancia = Mathf.Infinity;

        for (int i = 0; i < pontosDePatrulha.Count; i++)
        {
            float dist = Vector3.Distance(transform.position, pontosDePatrulha[i].position);
            if (dist < menorDistancia)
            {
                menorDistancia = dist;
                indiceMaisProximo = i;
            }
        }

        return indiceMaisProximo;
    }

    // ==================== TRIGGER ====================
    // Chamados pelo Collider Trigger (filho do inimigo)
    public void DetectarAlvo(Transform alvoDetectado)
    {
        if (alvoDetectado == null) return;
        alvo = alvoDetectado;
        MudarEstado(Estado.Perseguicao);
    }

    public void PerderAlvo(Transform alvoPerdido)
    {
        if (alvoPerdido == null) return;
        if (alvoPerdido != alvo) return;

        MudarEstado(Estado.Patrulha);
    }
}