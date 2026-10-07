using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class InimigoArea : MonoBehaviour
{
    public enum Estado { Parado, Patrulha, Perseguicao }

    [Header("Estado Atual (visualização)")]
    [SerializeField] private Estado estadoAtual = Estado.Parado;

    [Header("Comportamento Inicial")]
    [Tooltip("O que o inimigo faz enquanto o player não entrou na área")]
    public bool patrulharEnquantoEspera = true;

    [Header("Pontos de Patrulha (opcional)")]
    public Transform[] pontosDePatrulha;

    [Header("Configurações")]
    public float distanciaMinimaPonto = 0.5f;
    public float tempoDeEsperaNoPonto = 1f;
    public float velocidadePatrulha = 2f;
    public float velocidadePerseguicao = 5f;

    [Header("Ao Perder o Alvo")]
    [Tooltip("Tempo perseguindo o último ponto conhecido após perder o alvo")]
    public float tempoDeMemoria = 3f;

    private NavMeshAgent agent;
    private Transform alvo;
    private int indexPontoAtual = 0;
    private float tempoEsperando = 0f;
    private bool esperando = false;

    // Controle de memória
    private Vector3 ultimaPosicaoConhecida;
    private float tempoMemoriaRestante = 0f;
    private bool emMemoria = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (patrulharEnquantoEspera && pontosDePatrulha != null && pontosDePatrulha.Length > 0)
        {
            MudarEstado(Estado.Patrulha);
        }
        else
        {
            MudarEstado(Estado.Parado);
        }
    }

    void Update()
    {
        switch (estadoAtual)
        {
            case Estado.Parado:      AtualizarParado();      break;
            case Estado.Patrulha:    AtualizarPatrulha();    break;
            case Estado.Perseguicao: AtualizarPerseguicao(); break;
        }
    }

    // ==================== PARADO ====================
    private void AtualizarParado()
    {
        // Nada a fazer, apenas fica parado esperando
        // (poderia rodar uma animação de idle aqui)
    }

    // ==================== PATRULHA ====================
    private void AtualizarPatrulha()
    {
        if (pontosDePatrulha == null || pontosDePatrulha.Length == 0) return;

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

        if (!agent.pathPending && agent.remainingDistance <= distanciaMinimaPonto)
        {
            esperando = true;
            tempoEsperando = tempoDeEsperaNoPonto;
        }
    }

    private void IrParaProximoPonto()
    {
        if (pontosDePatrulha.Length == 0) return;
        indexPontoAtual = (indexPontoAtual + 1) % pontosDePatrulha.Length;
        agent.SetDestination(pontosDePatrulha[indexPontoAtual].position);
    }

    // ==================== PERSEGUIÇÃO ====================
    private void AtualizarPerseguicao()
    {
        if (alvo != null)
        {
            // Atualiza a última posição conhecida
            ultimaPosicaoConhecida = alvo.position;
            emMemoria = false;

            if (agent.isOnNavMesh)
                agent.SetDestination(ultimaPosicaoConhecida);
            return;
        }

        // Sem alvo: usa a memória
        if (emMemoria)
        {
            tempoMemoriaRestante -= Time.deltaTime;

            if (agent.isOnNavMesh)
                agent.SetDestination(ultimaPosicaoConhecida);

            bool chegou = !agent.pathPending && agent.remainingDistance <= distanciaMinimaPonto;
            if (tempoMemoriaRestante <= 0f || chegou)
            {
                emMemoria = false;
                VoltarAoComportamentoPadrao();
            }
        }
        else
        {
            VoltarAoComportamentoPadrao();
        }
    }

    private void VoltarAoComportamentoPadrao()
    {
        if (patrulharEnquantoEspera && pontosDePatrulha != null && pontosDePatrulha.Length > 0)
            MudarEstado(Estado.Patrulha);
        else
            MudarEstado(Estado.Parado);
    }

    // ==================== API PÚBLICA (chamada pela AreaDeAtivacao) ====================
    public void AtivarPerseguicao(Transform novoAlvo)
    {
        alvo = novoAlvo;
        emMemoria = false;
        MudarEstado(Estado.Perseguicao);
    }

    public void DesativarPerseguicao(Transform alvoQueSaiu)
    {
        if (alvoQueSaiu != alvo) return;

        // Guarda a última posição conhecida e entra em modo memória
        if (alvo != null)
            ultimaPosicaoConhecida = alvo.position;

        alvo = null;
        emMemoria = true;
        tempoMemoriaRestante = tempoDeMemoria;
    }

    // ==================== TROCA DE ESTADO ====================
    private void MudarEstado(Estado novoEstado)
    {
        if (estadoAtual == novoEstado) return;

        estadoAtual = novoEstado;
        esperando = false;

        switch (novoEstado)
        {
            case Estado.Parado:
                agent.isStopped = true;
                break;

            case Estado.Patrulha:
                agent.isStopped = false;
                agent.speed = velocidadePatrulha;
                if (pontosDePatrulha != null && pontosDePatrulha.Length > 0)
                {
                    indexPontoAtual = EncontrarPontoMaisProximo();
                    agent.SetDestination(pontosDePatrulha[indexPontoAtual].position);
                }
                break;

            case Estado.Perseguicao:
                agent.isStopped = false;
                agent.speed = velocidadePerseguicao;
                break;
        }
    }

    private int EncontrarPontoMaisProximo()
    {
        int indice = 0;
        float menor = Mathf.Infinity;
        for (int i = 0; i < pontosDePatrulha.Length; i++)
        {
            float d = Vector3.Distance(transform.position, pontosDePatrulha[i].position);
            if (d < menor) { menor = d; indice = i; }
        }
        return indice;
    }
}