using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Syncfusion.Blazor.Diagram;

namespace MermaidLiveEditor;

public static class JsonDiagramParser
{
    #region Constants and Counters
    private const double DEFAULT_NODE_WIDTH = 150;
    private const double DEFAULT_NODE_HEIGHT = 50;
    private static int annotationIdCounter = 0;
    private static int nodeIdCounter = 0;
    #endregion

    #region Main Processing Method
    /// <summary>
    /// Processes JSON data and converts it into diagram nodes and connectors
    /// </summary>
    /// <param name="jsonData">JSON string to be parsed</param>
    /// <returns>DiagramData containing nodes and connectors</returns>
    public static DiagramData ProcessData(string jsonData)
    {
        var diagramData = new DiagramData();
        ResetCounters();
        
        using var document = JsonDocument.Parse(jsonData);
        var rootElement = document.RootElement;

        // Handle direct array at root level
        if (rootElement.ValueKind == JsonValueKind.Array)
        {
            ProcessRootArray(rootElement, diagramData);
            HandleMultipleRoots(diagramData.Nodes, diagramData.Connectors);
            return diagramData;
        }

        // Handle empty or invalid JSON
        if (rootElement.ValueKind != JsonValueKind.Object || !HasAnyProperties(rootElement))
            return diagramData;

        // Process root object
        ProcessRootObject(rootElement, diagramData);
        
        return diagramData;
    }
    #endregion

    #region Root Level Processing
    /// <summary>
    /// Processes JSON when root is an array
    /// </summary>
    private static void ProcessRootArray(JsonElement arrayElement, DiagramData diagramData)
    {
        if (arrayElement.GetArrayLength() == 0)
            return;

        string rootNodeId = GenerateNodeId();
        var rootNode = CreateArrayNode(rootNodeId, arrayElement.GetArrayLength(), "Array", "Root");
        diagramData.Nodes.Add(rootNode);

        ProcessJsonElement(arrayElement, rootNodeId, diagramData.Nodes, diagramData.Connectors, "Root", "Array");
    }

    /// <summary>
    /// Processes JSON when root is an object
    /// </summary>
    private static void ProcessRootObject(JsonElement rootElement, DiagramData diagramData)
    {
        string rootNodeId = "root";
        JsonElement processedElement = rootElement;
        bool shouldSkipEmptyRoot = false;

        // Handle single property objects
        var rootProperties = GetObjectProperties(rootElement);
        if (rootProperties.Count == 1)
        {
            var singleProperty = rootProperties[0];
            if (IsEmptyPropertyName(singleProperty.Name) && singleProperty.Value.ValueKind == JsonValueKind.Object)
            {
                shouldSkipEmptyRoot = true;
                processedElement = singleProperty.Value;
            }
            else if (HasValidPropertyName(singleProperty.Name) && singleProperty.Value.ValueKind == JsonValueKind.Object)
            {
                rootNodeId = singleProperty.Name;
            }
        }

        // Separate primitive and complex properties
        var allProperties = GetObjectProperties(processedElement);
        var primitiveProperties = FilterPrimitiveProperties(allProperties);
        var complexProperties = FilterComplexProperties(allProperties);

        bool rootNodeCreated = false;
        string finalRootNodeId = shouldSkipEmptyRoot ? "data-root" : ConvertToNodeId(rootNodeId);

        // Create root node with primitive properties if any exist
        if (primitiveProperties.Any())
        {
            rootNodeCreated = true;
            var rootNode = CreatePrimitiveDataNode(finalRootNodeId, primitiveProperties, "Root");
            diagramData.Nodes.Add(rootNode);
            rootNodeId = finalRootNodeId;
        }

        // Process complex properties
        foreach (var property in complexProperties)
        {
            if (IsEmptyElement(property.Value))
                continue;

            ProcessComplexProperty(property, rootNodeId, rootNodeCreated, diagramData);
        }

        // Handle multiple root scenario
        if ((shouldSkipEmptyRoot || HasMultipleRootNodes(diagramData.Nodes, diagramData.Connectors)) && !rootNodeCreated)
        {
            HandleMultipleRoots(diagramData.Nodes, diagramData.Connectors);
        }
    }
    #endregion

    #region Property Processing
    /// <summary>
    /// Processes a complex property (object or array)
    /// </summary>
    private static void ProcessComplexProperty(JsonProperty property, string parentNodeId, bool parentExists, DiagramData diagramData)
    {
        var propertyKey = property.Name;
        var propertyElement = property.Value;
        var nodeId = ConvertToNodeId(propertyKey);
        var childCount = CalculateElementCount(propertyElement);

        var propertyNode = CreateComplexPropertyNode(nodeId, propertyKey, childCount, $"Root.{propertyKey}");
        diagramData.Nodes.Add(propertyNode);

        if (parentExists)
        {
            var connector = CreateConnector(parentNodeId, nodeId);
            diagramData.Connectors.Add(connector);
        }

        ProcessJsonElement(propertyElement, nodeId, diagramData.Nodes, diagramData.Connectors, $"Root.{propertyKey}", propertyKey);
    }
    #endregion

    #region JSON Element Processing
    /// <summary>
    /// Recursively processes JSON elements (arrays, objects, primitives)
    /// </summary>
    private static void ProcessJsonElement(
        JsonElement element,
        string parentNodeId,
        List<Node> nodeList,
        List<Connector> connectorList,
        string currentPath,
        string displayName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                ProcessArrayElement(element, parentNodeId, nodeList, connectorList, currentPath, displayName);
                break;
            case JsonValueKind.Object:
                ProcessObjectElement(element, parentNodeId, nodeList, connectorList, currentPath);
                break;
        }
    }

    /// <summary>
    /// Processes array elements
    /// </summary>
    private static void ProcessArrayElement(
        JsonElement arrayElement,
        string parentNodeId,
        List<Node> nodeList,
        List<Connector> connectorList,
        string currentPath,
        string displayName)
    {
        if (IsEmptyElement(arrayElement))
            return;

        int arrayIndex = 0;
        foreach (var arrayItem in arrayElement.EnumerateArray())
        {
            if (arrayItem.ValueKind == JsonValueKind.Null)
            {
                arrayIndex++;
                continue;
            }

            var itemNodeId = ConvertToNodeId($"{parentNodeId}-{arrayIndex}");
            var itemPath = $"{currentPath}[{arrayIndex}]";

            ProcessArrayItem(arrayItem, itemNodeId, parentNodeId, nodeList, connectorList, itemPath, arrayIndex);
            arrayIndex++;
        }
    }

    /// <summary>
    /// Processes individual array items
    /// </summary>
    private static void ProcessArrayItem(
        JsonElement arrayItem,
        string itemNodeId,
        string parentNodeId,
        List<Node> nodeList,
        List<Connector> connectorList,
        string itemPath,
        int arrayIndex)
    {
        if (IsPrimitiveElement(arrayItem))
        {
            ProcessPrimitiveArrayItem(arrayItem, itemNodeId, parentNodeId, nodeList, connectorList, itemPath);
        }
        else if (arrayItem.ValueKind == JsonValueKind.Array)
        {
            ProcessNestedArrayItem(arrayItem, itemNodeId, parentNodeId, nodeList, connectorList, itemPath, arrayIndex);
        }
        else if (arrayItem.ValueKind == JsonValueKind.Object)
        {
            ProcessObjectArrayItem(arrayItem, itemNodeId, parentNodeId, nodeList, connectorList, itemPath, arrayIndex);
        }
    }

    /// <summary>
    /// Processes primitive values in arrays
    /// </summary>
    private static void ProcessPrimitiveArrayItem(
        JsonElement primitiveItem,
        string itemNodeId,
        string parentNodeId,
        List<Node> nodeList,
        List<Connector> connectorList,
        string itemPath)
    {
        var formattedValue = FormatPrimitiveValue(primitiveItem.ToString());
        var primitiveNode = CreatePrimitiveValueNode(itemNodeId, formattedValue, itemPath);
        
        nodeList.Add(primitiveNode);
        connectorList.Add(CreateConnector(parentNodeId, itemNodeId));
    }

    /// <summary>
    /// Processes nested arrays within arrays
    /// </summary>
    private static void ProcessNestedArrayItem(
        JsonElement nestedArray,
        string itemNodeId,
        string parentNodeId,
        List<Node> nodeList,
        List<Connector> connectorList,
        string itemPath,
        int arrayIndex)
    {
        var childCount = CalculateElementCount(nestedArray);
        var arrayNode = CreateArrayItemNode(itemNodeId, arrayIndex, childCount, itemPath);
        
        nodeList.Add(arrayNode);
        connectorList.Add(CreateConnector(parentNodeId, itemNodeId));
        
        ProcessJsonElement(nestedArray, itemNodeId, nodeList, connectorList, itemPath, $"Item {arrayIndex}");
    }

    /// <summary>
    /// Processes objects within arrays
    /// </summary>
    private static void ProcessObjectArrayItem(
        JsonElement objectItem,
        string itemNodeId,
        string parentNodeId,
        List<Node> nodeList,
        List<Connector> connectorList,
        string itemPath,
        int arrayIndex)
    {
        var objectProperties = GetObjectProperties(objectItem);
        var primitiveProperties = FilterPrimitiveProperties(objectProperties);
        var complexProperties = FilterComplexProperties(objectProperties);

        bool requiresIntermediateNode = primitiveProperties.Any() || complexProperties.Count > 1;

        if (requiresIntermediateNode)
        {
            CreateIntermediateObjectNode(objectItem, itemNodeId, parentNodeId, nodeList, connectorList, itemPath, arrayIndex, primitiveProperties, complexProperties);
        }
        else if (complexProperties.Count == 1)
        {
            CreateDirectObjectConnection(complexProperties[0], parentNodeId, nodeList, connectorList, itemPath, arrayIndex);
        }
    }

    /// <summary>
    /// Creates intermediate node for complex objects in arrays
    /// </summary>
    private static void CreateIntermediateObjectNode(
        JsonElement objectItem,
        string itemNodeId,
        string parentNodeId,
        List<Node> nodeList,
        List<Connector> connectorList,
        string itemPath,
        int arrayIndex,
        List<JsonProperty> primitiveProperties,
        List<JsonProperty> complexProperties)
    {
        if (primitiveProperties.Any())
        {
            var objectNode = CreatePrimitiveDataNode(itemNodeId, primitiveProperties, itemPath);
            nodeList.Add(objectNode);
        }
        else
        {
            var itemNode = CreateArrayItemNode(itemNodeId, arrayIndex, 0, itemPath);
            nodeList.Add(itemNode);
        }

        connectorList.Add(CreateConnector(parentNodeId, itemNodeId));

        // Process complex child properties
        foreach (var complexProperty in complexProperties)
        {
            var childNodeId = ConvertToNodeId($"{itemNodeId}-{complexProperty.Name}");
            var childPath = $"{itemPath}.{complexProperty.Name}";
            var childCount = CalculateElementCount(complexProperty.Value);
            
            var childNode = CreateComplexPropertyNode(childNodeId, complexProperty.Name, childCount, childPath);
            nodeList.Add(childNode);
            connectorList.Add(CreateConnector(itemNodeId, childNodeId));
            
            ProcessJsonElement(complexProperty.Value, childNodeId, nodeList, connectorList, childPath, complexProperty.Name);
        }
    }

    /// <summary>
    /// Creates direct connection for single complex property
    /// </summary>
    private static void CreateDirectObjectConnection(
        JsonProperty singleComplexProperty,
        string parentNodeId,
        List<Node> nodeList,
        List<Connector> connectorList,
        string itemPath,
        int arrayIndex)
    {
        var childNodeId = ConvertToNodeId($"{parentNodeId}-{arrayIndex}-{singleComplexProperty.Name}");
        var childPath = $"{itemPath}.{singleComplexProperty.Name}";
        var childCount = CalculateElementCount(singleComplexProperty.Value);
        
        var childNode = CreateComplexPropertyNode(childNodeId, singleComplexProperty.Name, childCount, childPath);
        nodeList.Add(childNode);
        connectorList.Add(CreateConnector(parentNodeId, childNodeId));
        
        ProcessJsonElement(singleComplexProperty.Value, childNodeId, nodeList, connectorList, childPath, singleComplexProperty.Name);
    }

    /// <summary>
    /// Processes object elements
    /// </summary>
    private static void ProcessObjectElement(
        JsonElement objectElement,
        string parentNodeId,
        List<Node> nodeList,
        List<Connector> connectorList,
        string currentPath)
    {
        var objectProperties = GetObjectProperties(objectElement);
        var primitiveProperties = FilterPrimitiveProperties(objectProperties);
        var complexProperties = FilterComplexProperties(objectProperties);

        // Create leaf node for primitive properties
        if (primitiveProperties.Any())
        {
            var leafNodeId = ConvertToNodeId($"{parentNodeId}-leaf");
            var leafNode = CreatePrimitiveDataNode(leafNodeId, primitiveProperties, $"{currentPath}.leaf");
            
            nodeList.Add(leafNode);
            connectorList.Add(CreateConnector(parentNodeId, leafNodeId));
        }

        // Process complex properties
        foreach (var complexProperty in complexProperties)
        {
            var childNodeId = ConvertToNodeId($"{parentNodeId}-{complexProperty.Name}");
            var childPath = $"{currentPath}.{complexProperty.Name}";
            var childCount = CalculateElementCount(complexProperty.Value);
            
            var childNode = CreateComplexPropertyNode(childNodeId, complexProperty.Name, childCount, childPath);
            nodeList.Add(childNode);
            connectorList.Add(CreateConnector(parentNodeId, childNodeId));
            
            ProcessJsonElement(complexProperty.Value, childNodeId, nodeList, connectorList, childPath, complexProperty.Name);
        }
    }
    #endregion

    #region Node Creation Methods
    /// <summary>
    /// Creates a node for primitive data with key-value pairs
    /// </summary>
    private static Node CreatePrimitiveDataNode(string nodeId, List<JsonProperty> primitiveProperties, string nodePath)
    {
        var annotations = new DiagramObjectCollection<ShapeAnnotation>();
        var displayLines = new List<string>();

        foreach (var property in primitiveProperties)
        {
            var key = property.Name;
            var rawValue = property.Value.ToString();
            var formattedValue = FormatPrimitiveValue(rawValue);
            
            annotations.Add(new ShapeAnnotation { ID = $"Key_{++annotationIdCounter}", Content = $"{key}:" });
            annotations.Add(new ShapeAnnotation { ID = $"Value_{++annotationIdCounter}", Content = formattedValue });
            displayLines.Add($"{key}: {formattedValue}");
        }

        var mergedContent = string.Join("\n", displayLines);
        
        return new Node
        {
            ID = nodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = annotations,
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = true,
                ["mergedContent"] = mergedContent
            },
            Data = new { path = nodePath, title = mergedContent, actualdata = mergedContent }
        };
    }

    /// <summary>
    /// Creates a node for complex properties (objects/arrays)
    /// </summary>
    private static Node CreateComplexPropertyNode(string nodeId, string propertyName, int childCount, string nodePath)
    {
        var annotations = new DiagramObjectCollection<ShapeAnnotation>
        {
            new ShapeAnnotation { ID = $"Parent_{++annotationIdCounter}", Content = propertyName }
        };

        if (childCount > 0)
        {
            annotations.Add(new ShapeAnnotation { ID = $"Count_{++annotationIdCounter}", Content = $"{{{childCount}}}" });
        }

        var mergedContent = $"{propertyName}   {{{childCount}}}";
        
        return new Node
        {
            ID = nodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = annotations,
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = false,
                ["mergedContent"] = mergedContent
            },
            Data = new { path = nodePath, title = propertyName, actualdata = propertyName }
        };
    }

    /// <summary>
    /// Creates a node for array containers
    /// </summary>
    private static Node CreateArrayNode(string nodeId, int arrayLength, string displayName, string nodePath)
    {
        return new Node
        {
            ID = nodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { ID = $"Parent_{++annotationIdCounter}", Content = displayName },
                new ShapeAnnotation { ID = $"Count_{++annotationIdCounter}", Content = $"{{{arrayLength}}}" }
            },
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = false,
                ["mergedContent"] = $"{displayName} {{{arrayLength}}}"
            },
            Data = new { path = nodePath, title = displayName, actualdata = displayName }
        };
    }

    /// <summary>
    /// Creates a node for array items
    /// </summary>
    private static Node CreateArrayItemNode(string nodeId, int itemIndex, int childCount, string nodePath)
    {
        var annotations = new DiagramObjectCollection<ShapeAnnotation>
        {
            new ShapeAnnotation { ID = $"Parent_{++annotationIdCounter}", Content = $"Item {itemIndex}" }
        };

        if (childCount > 0)
        {
            annotations.Add(new ShapeAnnotation { ID = $"Count_{++annotationIdCounter}", Content = $"{{{childCount}}}" });
        }

        var mergedContent = $"Item {itemIndex} {{{childCount}}}";
        
        return new Node
        {
            ID = nodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = annotations,
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = false,
                ["mergedContent"] = mergedContent
            },
            Data = new { path = nodePath, title = $"Item {itemIndex}", actualdata = $"Item {itemIndex}" }
        };
    }

    /// <summary>
    /// Creates a node for primitive values
    /// </summary>
    private static Node CreatePrimitiveValueNode(string nodeId, string formattedValue, string nodePath)
    {
        return new Node
        {
            ID = nodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { ID = $"Value_{++annotationIdCounter}", Content = formattedValue }
            },
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = true,
                ["mergedContent"] = formattedValue
            },
            Data = new { path = nodePath, title = formattedValue, actualdata = formattedValue }
        };
    }

    /// <summary>
    /// Creates a connector between two nodes
    /// </summary>
    private static Connector CreateConnector(string sourceNodeId, string targetNodeId)
    {
        return new Connector
        {
            ID = $"connector-{sourceNodeId}-{targetNodeId}",
            SourceID = sourceNodeId,
            TargetID = targetNodeId
        };
    }
    #endregion

    #region Multiple Root Handling
    /// <summary>
    /// Checks if diagram has multiple root nodes
    /// </summary>
    private static bool HasMultipleRootNodes(List<Node> nodeList, List<Connector> connectorList)
    {
        var allNodeIds = nodeList.Select(node => node.ID).ToList();
        var nodesWithIncomingConnections = new HashSet<string>(connectorList.Select(connector => connector.TargetID));
        var rootNodes = allNodeIds.Where(nodeId => !nodesWithIncomingConnections.Contains(nodeId)).ToList();
        
        return rootNodes.Count > 1;
    }

    /// <summary>
    /// Handles multiple root nodes by creating a main root
    /// </summary>
    private static void HandleMultipleRoots(List<Node> nodeList, List<Connector> connectorList)
    {
        var allNodeIds = nodeList.Select(node => node.ID).ToList();
        var nodesWithIncomingConnections = new HashSet<string>(connectorList.Select(connector => connector.TargetID));
        var rootNodes = allNodeIds.Where(nodeId => !nodesWithIncomingConnections.Contains(nodeId)).ToList();

        if (rootNodes.Count > 1)
        {
            const string mainRootId = "main-root";
            var mainRootNode = new Node
            {
                ID = mainRootId,
                Width = 40,
                Height = 40,
                Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation { ID = $"Parent_{++annotationIdCounter}", Content = "" }
                },
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["isLeaf"] = false
                },
                Data = new { path = "MainRoot", title = "", actualdata = "" }
            };
            
            nodeList.Add(mainRootNode);

            foreach (var rootNodeId in rootNodes)
            {
                connectorList.Add(CreateConnector(mainRootId, rootNodeId));
            }
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// Resets all counters for fresh parsing
    /// </summary>
    private static void ResetCounters()
    {
        annotationIdCounter = 0;
        nodeIdCounter = 0;
    }

    /// <summary>
    /// Generates a sequential node ID
    /// </summary>
    private static string GenerateNodeId()
    {
        return $"{++nodeIdCounter}";
    }

    /// <summary>
    /// Converts any string to a valid node ID
    /// </summary>
    private static string ConvertToNodeId(string input)
    {
        return GenerateNodeId();
    }

    /// <summary>
    /// Gets all properties from a JSON object
    /// </summary>
    private static List<JsonProperty> GetObjectProperties(JsonElement objectElement)
    {
        return objectElement.EnumerateObject().ToList();
    }

    /// <summary>
    /// Filters properties to get only primitive ones
    /// </summary>
    private static List<JsonProperty> FilterPrimitiveProperties(List<JsonProperty> properties)
    {
        return properties.Where(property => 
            property.Value.ValueKind != JsonValueKind.Object && 
            property.Value.ValueKind != JsonValueKind.Array).ToList();
    }

    /// <summary>
    /// Filters properties to get only complex ones (objects/arrays)
    /// </summary>
    private static List<JsonProperty> FilterComplexProperties(List<JsonProperty> properties)
    {
        return properties.Where(property => 
            (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array) && 
            !IsEmptyElement(property.Value)).ToList();
    }

    /// <summary>
    /// Checks if an object has any properties
    /// </summary>
    private static bool HasAnyProperties(JsonElement objectElement)
    {
        return objectElement.EnumerateObject().Any();
    }

    /// <summary>
    /// Checks if a property name is empty or whitespace
    /// </summary>
    private static bool IsEmptyPropertyName(string propertyName)
    {
        return string.IsNullOrWhiteSpace(propertyName);
    }

    /// <summary>
    /// Checks if a property name is valid (not empty)
    /// </summary>
    private static bool HasValidPropertyName(string propertyName)
    {
        return !string.IsNullOrWhiteSpace(propertyName);
    }

    /// <summary>
    /// Checks if a JSON element is primitive (not object or array)
    /// </summary>
    private static bool IsPrimitiveElement(JsonElement element)
    {
        return element.ValueKind != JsonValueKind.Object && element.ValueKind != JsonValueKind.Array;
    }

    /// <summary>
    /// Calculates the logical count of child elements
    /// </summary>
    private static int CalculateElementCount(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.GetArrayLength();
            
        if (element.ValueKind != JsonValueKind.Object)
            return 0;

        var properties = GetObjectProperties(element);
        var primitiveCount = FilterPrimitiveProperties(properties).Count > 0 ? 1 : 0;
        var arrayCount = properties.Count(p => p.Value.ValueKind == JsonValueKind.Array);
        var objectCount = properties.Count(p => p.Value.ValueKind == JsonValueKind.Object);
        
        return primitiveCount + arrayCount + objectCount;
    }

    /// <summary>
    /// Formats primitive values for display
    /// </summary>
    private static string FormatPrimitiveValue(string rawValue)
    {
        if (bool.TryParse(rawValue, out var boolValue))
            return boolValue.ToString().ToLower();
            
        if (double.TryParse(rawValue, out var numericValue))
            return numericValue.ToString();
            
        return $"\"{rawValue}\"";
    }

    /// <summary>
    /// Checks if a JSON element is empty (empty array or object)
    /// </summary>
    private static bool IsEmptyElement(JsonElement element)
    {
        return (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 0) ||
               (element.ValueKind == JsonValueKind.Object && !element.EnumerateObject().Any());
    }
    #endregion
}

#region Data Models
public class DiagramData
{
    public List<Node> Nodes { get; set; } = new List<Node>();
    public List<Connector> Connectors { get; set; } = new List<Connector>();
}

public enum EditorInputType
{
    json,
    xml
}
#endregion