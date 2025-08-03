using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Linq;
using TMPro;

public class DilemmaBehavior : MonoBehaviour
{
    [SerializeField] private GameObject agentPrefab;
    [SerializeField] private GameObject edgePrefab;
    [SerializeField] private GameObject backgroundObject;
    [SerializeField] private Button generationButton;
    [SerializeField] private Button pruneButton;
    [SerializeField] public VerticalLayoutGroup AgentInfoPanel;

    private static Dictionary<string, List<string>> completeMatchHistory = new Dictionary<string, List<string>>();

    public static DilemmaBehavior Instance;

    public GenerationResponse generationData;

    public static int CurrentGeneration = 0;

    private List<GameObject> agents = new List<GameObject>();
    private List<GameObject> activeEdges = new List<GameObject>();

    private List<(int, int)> edges;
    public static List<(string, int)> agentBehaviorList = new List<(string, int)>(); // Public static as we need to access it in our AgentScript
    private Dictionary<string, float> sortedTopFitness = new Dictionary<string, float>();
    private Dictionary<int, int> oldIndexToNewIndex = new Dictionary<int, int>();

    [SerializeField] private float jitterStrength = 22f; // Variable that scatters our agents (without this, they'll arrange themselves into a perfect circle)

    private void Awake()
    {
        ConfigureVerticalLayoutGroup();
        Instance = this;
    }
    private void Start()
    {
        LoadGraph();
        SpawnAgents();
        StartCoroutine(SpawnModels());
        DrawEdges();
        generationButton.onClick.AddListener(OnGenerationProgressionClicked);
        pruneButton.onClick.AddListener(PruneGeneration);

    }
    void LoadGraph()
    {
        TextAsset textAsset = Resources.Load<TextAsset>("ws_graph_edges"); // The JSON file we initialized under the generate_agent_graph python processes script
        edges = JsonConvert.DeserializeObject<List<List<int>>>(textAsset.text).ConvertAll(edge => (edge[0], edge[1]));
        Debug.Log($"Loaded {edges.Count} edges");
    }

    void SpawnAgents()
    {
        int numAgents = 400;
        float radius = 10f;
         
        for(int i = 0; i < numAgents; i++){
            float angle = i * Mathf.PI * 2 / numAgents;
            Vector2 basePos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector2 jitter = Random.insideUnitCircle * jitterStrength;
            Vector2 pos = basePos + jitter;

            GameObject agent = Instantiate(agentPrefab, new Vector3(pos.x, pos.y, 0), Quaternion.identity);
            agent.name = $"Agent_{i}";
            agents.Add(agent);
            agent.GetComponent<AgentScript>().agentID = $"agent_{i}";
        }
        AgentScript.allAgents = FindObjectsOfType<AgentScript>();
    }
    // I know my naming isn't the best, but the difference between SpawnModels and SpawnAgents is that this simply initializes the random pytorch models on our flask server
    IEnumerator SpawnModels()
    {
        string jsonRequest = $"{{\"num_agents\": {agents.Count}}}";
        
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequest);
        using (UnityWebRequest fetchSpawn = new UnityWebRequest("http://localhost:8080/models_init", "POST"))
        {
            fetchSpawn.uploadHandler = new UploadHandlerRaw(bodyRaw);
            fetchSpawn.downloadHandler = new DownloadHandlerBuffer();
            fetchSpawn.SetRequestHeader("Content-Type", "application/json");
            
            yield return fetchSpawn.SendWebRequest();
            
            if(fetchSpawn.result != UnityWebRequest.Result.Success){
                Debug.LogError($"Model init failed: {fetchSpawn.error}");
            }
            else{
                Debug.Log("Models successfully initialized on server!");
                Debug.Log($"Server returned: {fetchSpawn.downloadHandler.text}"); // This bit is honestly just to confirm that the correct number of agents have been created on the server
            }
        }
    }

    void DrawEdges()
    {
        foreach(var (a, b) in edges){
            Vector3 posA = agents[a].transform.position;
            Vector3 posB = agents[b].transform.position;

            GameObject edge = Instantiate(edgePrefab);
            activeEdges.Add(edge);
            var lineRenderer = edge.GetComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, new Vector3(posA.x, posA.y, 0f));
            lineRenderer.SetPosition(1, new Vector3(posB.x, posB.y, 0f));
            lineRenderer.startWidth = 0.05f;
            lineRenderer.endWidth = 0.05f;

            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.gray;
            lineRenderer.endColor = Color.gray;

            lineRenderer.sortingLayerName = "Edges";  
            lineRenderer.sortingOrder = 0;

            var agentA = agents[a].GetComponent<AgentScript>();
            var agentB = agents[b].GetComponent<AgentScript>();

            if(agentA && agentB){
                agentA.connectedAgents.Add(agents[b]);
                agentB.connectedAgents.Add(agents[a]);

                agentA.connectedEdges.Add(lineRenderer);
                agentB.connectedEdges.Add(lineRenderer);
            }
        }
        AgentScript.allEdges = FindObjectsOfType<LineRenderer>();
    }
    void DrawPrunedEdges()
    {
        foreach(var (oldA, oldB) in edges){
            if(oldIndexToNewIndex.TryGetValue(oldA, out int newA) && oldIndexToNewIndex.TryGetValue(oldB, out int newB)){
                Vector3 posA = agents[newA].transform.position;
                Vector3 posB = agents[newB].transform.position;

                GameObject edge = Instantiate(edgePrefab);
                activeEdges.Add(edge);
                var lineRenderer = edge.GetComponent<LineRenderer>();
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, new Vector3(posA.x, posA.y, 0f));
                lineRenderer.SetPosition(1, new Vector3(posB.x, posB.y, 0f));
                lineRenderer.startWidth = 0.05f;
                lineRenderer.endWidth = 0.05f;

                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = Color.gray;
                lineRenderer.endColor = Color.gray;

                lineRenderer.sortingLayerName = "Edges";  
                lineRenderer.sortingOrder = 0;

                var agentA = agents[newA].GetComponent<AgentScript>();
                var agentB = agents[newB].GetComponent<AgentScript>();

                if(agentA && agentB){
                    agentA.connectedAgents.Add(agents[newB]);
                    agentB.connectedAgents.Add(agents[newA]);

                    agentA.connectedEdges.Add(lineRenderer);
                    agentB.connectedEdges.Add(lineRenderer);
                }
            }
        }
        AgentScript.allEdges = FindObjectsOfType<LineRenderer>();
    }
    void OnGenerationProgressionClicked()
    {
        Debug.Log("Generation Progression button clicked!");
        StartCoroutine(ProgressGeneration());
    }
    IEnumerator ProgressGeneration()
    {
        if(CurrentGeneration > 0){
            // The reason why we don't need any code to "handle" mutation logic other than this, is because our mutations are stored sever side! 
            // This means we can seamlessly integrate this method into our script
            yield return StartCoroutine(MutateGeneration()); // Make sure our mutation completes first
            ClearSim();

            // Reset static references
            AgentScript.allAgents = null; 
            AgentScript.allEdges = null; 

            LoadGraph();
            SpawnAgents();
            DrawEdges();

            yield return new WaitForEndOfFrame(); // Wait for all agents & edges to be created
            
            // Ensure our static references are properly set after recreation (so pruned agents don't ONLY show match history against other pruned agents)
            AgentScript.allAgents = FindObjectsOfType<AgentScript>();
            AgentScript.allEdges = FindObjectsOfType<LineRenderer>();
        }
        
        using (UnityWebRequest fetchGeneration = UnityWebRequest.Get("http://localhost:8080/play_dilemma"))
        {
            yield return fetchGeneration.SendWebRequest();

            if(fetchGeneration.result != UnityWebRequest.Result.Success){
                Debug.LogError($"Generation progression failed: {fetchGeneration.error}");
            }
            else{
                string responseText = fetchGeneration.downloadHandler.text;
                Debug.Log("Generation progression successful!");
                Debug.Log($"Server returned: {responseText}");
                generationData = JsonConvert.DeserializeObject<GenerationResponse>(responseText);

                // Store complete match history before any pruning happens
                StoreCompleteMatchHistory();

                // GenerationData includes the following: 
                // generationData.actions
                // generationData.fitness_scores
                // generationData.general_scores

                // Who we'll keep from this generation.
                // Now I contemplated between progressing generations either based on global score or between score among smaller subsets of say, 4 agents.
                // I chose global progression as it is, ahem, easier to do, and that we can still obtain variance akin to local tournaments given the variety of opponents each agent plays due to our Watts-Strogatz network. 
                sortedTopFitness = generationData.fitness_scores.OrderByDescending(pair => pair.Value).Take((int)(generationData.fitness_scores.Count * 0.25)).ToDictionary(pair => pair.Key, pair => pair.Value);
                
                // For visualization, we'll save if certain agents lean more towards being stealers/splitters. 
                agentBehaviorList.Clear();
                foreach(var agentActions in generationData.actions){
                    int zeros = agentActions.Value.Count(actions => actions == 0);
                    int ones = agentActions.Value.Count(actions => actions == 1);
                    int behavior = (zeros >= ones) ? 0 : 1;
                    agentBehaviorList.Add((agentActions.Key, behavior));
                }
                // Without this line/logic we would encounter an error where whenever an agent is clicked off, their color turns gray. 
                AgentScript.agentBehaviorList = agentBehaviorList;

                // Now we'll loop through our agent GameObjects and set their color to Blue(Leans more towards splitting) and Red(Leans more towards stealing)
                foreach(var agent in agents){
                    var agentScript = agent.GetComponent<AgentScript>();
                    string agentId = agentScript.agentID;

                    var behaviorEntry = agentBehaviorList.FirstOrDefault(entry => entry.Item1 == agentId);

                    if(behaviorEntry != default){
                        Color colorToSet = (behaviorEntry.Item2 == 0) ? Color.blue : Color.red;
                        agentScript.SetColor(colorToSet);
                    }
                }
                CurrentGeneration++;
            }
        }
    }
    void StoreCompleteMatchHistory() // Method to avoid pruning agents match history pitfall
    {
        if(generationData == null || generationData.actions == null)
            return;

        foreach(var agent in agents){
            var agentScript = agent.GetComponent<AgentScript>();
            if(agentScript == null) continue;

            string agentKey = agentScript.agentID;
            
            if(!generationData.actions.ContainsKey(agentKey))
                continue;

            List<int> actions = generationData.actions[agentKey];
            var connectedAgents = agentScript.connectedAgents;
            
            if(connectedAgents == null || connectedAgents.Count == 0)
                continue;

            List<string> matchHistory = new List<string>();
            int roundsPerOpponent = 4;
            
            for(int i = 0; i < connectedAgents.Count; i++){
                var opponentScript = connectedAgents[i].GetComponent<AgentScript>();
                if(opponentScript == null) continue;

                string opponentID = opponentScript.agentID;
                int opponentIndex = ExtractIndexFromAgentID(opponentID);
                
                List<string> roundActions = new List<string>();
                for(int r = 0; r < roundsPerOpponent; r++) 
                {
                    int actionIndex = i * roundsPerOpponent + r;
                    if(actionIndex >= actions.Count) break;

                    int action = actions[actionIndex];
                    roundActions.Add(action == 0 ? "Split" : "Steal");
                }
                
                // Store opponent info and their actions
                matchHistory.Add($"vs Agent {opponentIndex}:{string.Join(",", roundActions)}");
            }
            
            completeMatchHistory[agentKey] = matchHistory;
        }
    }
    void PruneGeneration()
    {
        if(CurrentGeneration >= 1){
            var agentIDsToKeep = new HashSet<string>(sortedTopFitness.Keys);
            List<GameObject> prunedAgents = new List<GameObject>();
            // Destroy all edges for all of our agents before pruning 

            foreach(var agent in agents){
                var agentScript = agent.GetComponent<AgentScript>();
                foreach(var edgeLine in agentScript.connectedEdges){
                    if(edgeLine != null){
                        Destroy(edgeLine.gameObject);
                    }
                }
                agentScript.connectedEdges.Clear();
                agentScript.connectedAgents.Clear();
            }

            AgentScript.allAgents = null;
            AgentScript.allEdges = null;

            // We'll remove agents that didn't make it into our top 25% fitness rankings
            foreach(var agent in agents){
                var agentScript = agent.GetComponent<AgentScript>();
                if(agentScript != null && agentIDsToKeep.Contains(agentScript.agentID)){
                    prunedAgents.Add(agent);
                }
                else{
                    Destroy(agent);
                }
            }
            agents = prunedAgents;

            // Let's now build up oldIndexToNewIndex map for the newly pruned list
            oldIndexToNewIndex.Clear();
            for(int newIndex = 0; newIndex < agents.Count; newIndex++){
                string agentID = agents[newIndex].GetComponent<AgentScript>().agentID;
                int oldIndex = ExtractIndexFromAgentID(agentID);
                if(oldIndex >= 0){
                    oldIndexToNewIndex[oldIndex] = newIndex;
                }
            }

            agentBehaviorList.Clear();
            AgentScript.agentBehaviorList.Clear();

            DrawPrunedEdges();
        }
    }
    private static int ExtractIndexFromAgentID(string agentID)
    {
        var parts = agentID.Split('_');
        if(parts.Length == 2 && int.TryParse(parts[1], out int index))
            return index;
        return -1; 
    }

    IEnumerator MutateGeneration(){
        List<string> prunedAgentIDs = new List<string>();
        foreach(var agent in agents){
            var agentScript = agent.GetComponent<AgentScript>();
            if(agentScript != null){
                prunedAgentIDs.Add(agentScript.agentID);
            }
        }
        string jsonData = JsonConvert.SerializeObject(prunedAgentIDs);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest fetchMutation = new UnityWebRequest("http://localhost:8080/mutate_generation", "POST"))
        {
            fetchMutation.uploadHandler = new UploadHandlerRaw(bodyRaw);
            fetchMutation.downloadHandler = new DownloadHandlerBuffer();
            fetchMutation.SetRequestHeader("Content-Type", "application/json");

            yield return fetchMutation.SendWebRequest();

            if(fetchMutation.result != UnityWebRequest.Result.Success){
                Debug.LogError($"Mutation failed: {fetchMutation.error}");
            }
            else{
                Debug.Log("Mutation successful!");
                Debug.Log($"Server returned: {fetchMutation.downloadHandler.text}");
            }
        }
    }
    void ClearSim()
    {
        foreach(var edge in activeEdges){
            if(edge != null)
                Destroy(edge);
        }
        activeEdges.Clear();

        foreach(var agent in agents){
            if(agent != null)
                Destroy(agent);
        }
        agents.Clear();

        agentBehaviorList.Clear();
        AgentScript.agentBehaviorList.Clear();
        AgentScript.lastSelectedEdges.Clear();
        AgentScript.lastSelectedAgents.Clear();
        AgentScript.allAgents = null; 
        AgentScript.allEdges = null; 
    }
    public static void CreateDescription(int agentID)
    {
        string agentKey = $"agent_{agentID}";

        List<Transform> childrenToDestroy = new List<Transform>();
        foreach(Transform child in Instance.AgentInfoPanel.transform){
            childrenToDestroy.Add(child);
        }
        
        foreach(Transform child in childrenToDestroy){
            GameObject.DestroyImmediate(child.gameObject);
        }

        // Force our layout to rebuild and wait a frame (before this our text would stack weirdly and not clear itself between agent clicks)
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(Instance.AgentInfoPanel.GetComponent<RectTransform>());

        Instance.AddAgentTitle($"Agent {agentID}");
        
        // Use stored complete match history instead of current connections (pruned agent match history pitfall)
        if(!completeMatchHistory.ContainsKey(agentKey) || completeMatchHistory[agentKey].Count == 0){
            Debug.LogWarning($"No complete match history found for {agentKey}");
            Instance.AddAgentSubheader("No match history available for this agent yet.");
            return;
        }
        
        List<string> matchHistory = completeMatchHistory[agentKey];
        
        foreach(string matchInfo in matchHistory){
            // Parse our stored match info: "vs Agent X:Split,Steal,Split,Steal"
            string[] parts = matchInfo.Split(':');
            if(parts.Length == 2){
                string opponentInfo = parts[0]; // "vs Agent X"
                string[] actions = parts[1].Split(','); // ["Split", "Steal", "Split", "Steal"]
                
                Instance.AddAgentSubheader(opponentInfo);
                Instance.AddAgentActions(actions.ToList());
            }
        }
        
        // Force another layout rebuild after adding all content just to make sure yk
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(Instance.AgentInfoPanel.GetComponent<RectTransform>());
    }
    public void AddAgentTitle(string title)
    {
        TMP_Text textObj = CreateTMPText();
        textObj.text = title;
        textObj.fontSize = 12;
        textObj.fontStyle = FontStyles.Bold;
        textObj.color = Color.white;
    }

    public void AddAgentSubheader(string subheader)
    {
        TMP_Text textObj = CreateTMPText();
        textObj.text = subheader;
        textObj.fontSize = 8;
        textObj.fontStyle = FontStyles.Italic;
        textObj.color = new Color(0.85f, 0.85f, 0.85f); 
    }

    public void AddAgentActions(List<string> actions)
    {
        TMP_Text textObj = CreateTMPText();
        textObj.text = string.Join(", ", actions);
        textObj.fontSize = 4;
        textObj.color = Color.white;
    }
    
    private TMP_Text CreateTMPText()
    {
        GameObject textGO = new GameObject("DynamicTMPText");
        textGO.transform.SetParent(AgentInfoPanel.transform, false);

        TMP_Text tmpText = textGO.AddComponent<TextMeshProUGUI>();
        
        tmpText.enableAutoSizing = false;
        tmpText.fontSize = 20;
        tmpText.alignment = TextAlignmentOptions.TopLeft;
        
        tmpText.margin = Vector4.zero;
        tmpText.lineSpacing = 0f;
        tmpText.paragraphSpacing = 0f;
        tmpText.enableWordWrapping = false;
        
        RectTransform rectTransform = textGO.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(0, 1);
        
        tmpText.overflowMode = TextOverflowModes.Overflow;
        tmpText.verticalAlignment = VerticalAlignmentOptions.Top;
        
        LayoutElement layoutElement = textGO.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = -1; 
        layoutElement.flexibleHeight = 0;  
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        
        return tmpText;
    }
    void ConfigureVerticalLayoutGroup()
    {
        VerticalLayoutGroup vlg = AgentInfoPanel;
        vlg.spacing = 1.5f;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childScaleHeight = false;
        vlg.childScaleWidth = false;
    }

}


public class GenerationResponse{ // Just a little custom type for QOL ig
    public string message;
    public Dictionary<string, List<int>> actions;
    public Dictionary<string, float> fitness_scores;
    public Dictionary<string, int> general_scores;
}