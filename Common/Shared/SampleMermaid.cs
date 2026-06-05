using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorServerCommon_NET8.Shared
{

    // ─────────────────────────────────────────────
    // Sample Mermaid snippets
    // Mirrors sampleMermaid.ts in React
    // ─────────────────────────────────────────────
    public static class SampleMermaid
    {
        public const string SAMPLE_FLOWCHART = @"flowchart TD
A[Start] --> B(Process)
B -.- C{Decision}
C -- Yes --> D[Plan 1]
C ==>|No| E[Plan 2]
style A fill:#90EE90,stroke:#333,stroke-width:2px;
style B fill:#4682B4,stroke:#333,stroke-width:2px;
style C fill:#FFD700,stroke:#333,stroke-width:2px;
style D fill:#FF6347,stroke:#333,stroke-width:2px;
style E fill:#FF6347,stroke:#333,stroke-width:2px;";

        public const string SAMPLE_SEQUENCE = @"sequenceDiagram
participant User
participant Controller
participant Service
participant Database

User->>Controller: sendRequest()
activate Controller

Controller->>Service: processRequest()
activate Service

Service->>Database: queryData()
activate Database
Database-->>Service: returnData()
deactivate Database

Service-->>Controller: returnResponse()
deactivate Service

Controller-->>User: sendResponse()
deactivate Controller";

        public const string SAMPLE_MINDMAP = @"mindmap
root((mindmap))
  Origins
    Long history
    ::icon(fa fa-book)
    Popularisation
      British popular psychology author Tony Buzan
  Research
    On effectiveness<br/>and features
    On Automatic creation
      Uses
        Creative techniques
        Strategic planning
        Argument mapping
  Tools
    Pen and paper
    Mermaid";

        // ─────────────────────────────────────────────
        // Typed sample keys (React SampleKey equivalent)
        // ─────────────────────────────────────────────
        public enum SampleKey
        {
            SampleFlowchart,
            SampleSequence,
            SampleMindmap
        }

        // ─────────────────────────────────────────────
        // All samples dictionary (React SAMPLES)
        // ─────────────────────────────────────────────
        public static readonly IReadOnlyDictionary<SampleKey, string> SAMPLES =
            new Dictionary<SampleKey, string>
            {
            { SampleKey.SampleFlowchart, SAMPLE_FLOWCHART },
            { SampleKey.SampleSequence, SAMPLE_SEQUENCE },
            { SampleKey.SampleMindmap, SAMPLE_MINDMAP }
            };

        // ─────────────────────────────────────────────
        // Default sample (React DEFAULT_SAMPLE)
        // ─────────────────────────────────────────────
        public const string DEFAULT_SAMPLE = SAMPLE_FLOWCHART;
    }

}
