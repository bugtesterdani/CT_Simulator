const state = {
  graph: {
    version: "1.0",
    metadata: { title: "Untitled Graph", sourceFormat: "blockgraph", tags: {} },
    nodes: [],
    edges: [],
  },
  selectedNodeId: null,
  selectedPortRef: null,
  drag: null,
};

const status = document.getElementById("status");
const nodeList = document.getElementById("nodeList");
const edgeList = document.getElementById("edgeList");
const selectedNodeLabel = document.getElementById("selectedNode");
const selectedNodeBanner = document.getElementById("selectedNodeBanner");
const selectedNodeValue = document.getElementById("selectedNodeValue");
const portList = document.getElementById("portList");
const portName = document.getElementById("portName");
const portIndex = document.getElementById("portIndex");
const edgeColorPicker = document.getElementById("edgeColor");
const connectFromLabel = document.getElementById("connectFrom");
const connectToLabel = document.getElementById("connectTo");
const cancelConnectButton = document.getElementById("cancelConnect");
const board = document.getElementById("board");
const boardContent = document.getElementById("boardContent");
const edgeLayer = document.getElementById("edgeLayer");
const themeToggle = document.getElementById("toggleTheme");
const propKey = document.getElementById("propKey");
const propKeyList = document.getElementById("propKeyList");
const propHint = document.getElementById("propHint");
const propValueTextWrap = document.getElementById("propValueTextWrap");
const propValueText = document.getElementById("propValueText");
const propValueBoolWrap = document.getElementById("propValueBoolWrap");
const propValueBool = document.getElementById("propValueBool");
const propValueArrayWrap = document.getElementById("propValueArrayWrap");
const propValueArrayItem = document.getElementById("propValueArrayItem");
const propValueArrayAdd = document.getElementById("propValueArrayAdd");
const propValueArrayList = document.getElementById("propValueArrayList");
const addProp = document.getElementById("addProp");
const propList = document.getElementById("propList");
const metaJson = document.getElementById("metaJson");
const optionsJson = document.getElementById("optionsJson");
const tweakJson = document.getElementById("tweakJson");
const bomJson = document.getElementById("bomJson");
const saveGlobals = document.getElementById("saveGlobals");
const exportCompress = document.getElementById("exportCompress");
const exportFull = document.getElementById("exportFull");
const exportSnapshot = document.getElementById("exportSnapshot");
const previewAuto = document.getElementById("previewAuto");
const refreshPreview = document.getElementById("refreshPreview");
const yamlPreview = document.getElementById("yamlPreview");
const appMain = document.getElementById("appMain");
const resizerLeft = document.getElementById("resizerLeft");
const resizerRight = document.getElementById("resizerRight");
const zoomOut = document.getElementById("zoomOut");
const zoomIn = document.getElementById("zoomIn");
const resetView = document.getElementById("resetView");
const zoomLevel = document.getElementById("zoomLevel");

let connectorKeys = [];
let cableKeys = [];
let propTypeHints = {};

const viewState = {
  zoom: 1,
  offsetX: 0,
  offsetY: 0,
  panning: false,
  panStartX: 0,
  panStartY: 0,
};

function setStatus(text) {
  if (!status) return;
  status.textContent = text;
}

function render() {
  if (!nodeList || !edgeList || !board || !edgeLayer) {
    setStatus("UI incomplete. Please reload.");
    return;
  }
  renderNodes();
  renderBoard();
  renderEdges();
  renderPorts();
  schedulePreviewRefresh();
}

function initResizers() {
  if (!appMain || !resizerLeft || !resizerRight) {
    return;
  }

  const stored = localStorage.getItem("ct3xx-ui-columns");
  if (stored) {
    appMain.style.gridTemplateColumns = stored;
  }

  resizerLeft.addEventListener("mousedown", (event) => beginResize(event, "left"));
  resizerRight.addEventListener("mousedown", (event) => beginResize(event, "right"));
}

function beginResize(event, side) {
  event.preventDefault();
  const startX = event.clientX;
  const columns = getComputedStyle(appMain).gridTemplateColumns.split(" ");
  const leftWidth = parseFloat(columns[0]);
  const boardWidth = parseFloat(columns[2]);
  const rightWidth = parseFloat(columns[4]);

  function onMove(moveEvent) {
    const delta = moveEvent.clientX - startX;
    if (side === "left") {
      const newLeft = Math.max(200, leftWidth + delta);
      const newBoard = Math.max(300, boardWidth - delta);
      appMain.style.gridTemplateColumns = `${newLeft}px 10px ${newBoard}px 10px ${rightWidth}px`;
    } else {
      const newRight = Math.max(200, rightWidth - delta);
      const newBoard = Math.max(300, boardWidth + delta);
      appMain.style.gridTemplateColumns = `${leftWidth}px 10px ${newBoard}px 10px ${newRight}px`;
    }
  }

  function onUp() {
    localStorage.setItem("ct3xx-ui-columns", appMain.style.gridTemplateColumns);
    window.removeEventListener("mousemove", onMove);
    window.removeEventListener("mouseup", onUp);
  }

  window.addEventListener("mousemove", onMove);
  window.addEventListener("mouseup", onUp);
}

function updateZoom() {
  if (zoomLevel) {
    zoomLevel.textContent = `${Math.round(viewState.zoom * 100)}%`;
  }
  renderBoard();
}

function renderNodes() {
  nodeList.innerHTML = "";
  state.graph.nodes.forEach((node) => {
    const card = document.createElement("div");
    card.className = "node-card";
    if (state.selectedNodeId === node.id) {
      card.classList.add("selected");
    }
    card.innerHTML = `
      <header>
        <span>${node.name}</span>
        <small>${node.type}</small>
      </header>
      <div>Ports: ${node.ports.length}</div>
    `;
    card.addEventListener("click", () => selectNode(node.id));
    nodeList.appendChild(card);
  });
}

function renderBoard() {
  const existing = boardContent.querySelectorAll(".node-block");
  existing.forEach((el) => el.remove());
  boardContent.style.transform = `translate(${viewState.offsetX}px, ${viewState.offsetY}px) scale(${viewState.zoom})`;
  state.graph.nodes.forEach((node) => {
    const block = document.createElement("div");
    block.className = "node-block";
    if (state.selectedNodeId === node.id) {
      block.classList.add("selected");
    }
    block.style.left = `${node.x || 40}px`;
    block.style.top = `${node.y || 40}px`;
    block.dataset.nodeId = node.id;
    const ports = node.ports
      .map(
        (p) =>
          `<li><span class="port-dot" data-node="${node.id}" data-port="${p.id}"><span class="dot"></span>${p.name}</span></li>`
      )
      .join("");
    block.innerHTML = `<h3>${node.name}</h3><ul>${ports}</ul>`;
    block.addEventListener("click", (event) => {
      event.stopPropagation();
      selectNode(node.id);
    });
    block.addEventListener("mousedown", (event) => beginDrag(event, node));
    block.querySelectorAll(".port-dot").forEach((dot) => {
      const nodeId = dot.getAttribute("data-node");
      const portId = dot.getAttribute("data-port");
      if (nodeId && portId && state.selectedPortRef === `${nodeId}:${portId}`) {
        dot.classList.add("selected");
      }
      dot.addEventListener("click", (event) => {
        event.stopPropagation();
        if (nodeId && portId) {
          selectPort(nodeId, portId);
        }
      });
    });
    boardContent.appendChild(block);
  });
  drawEdges();
}

function renderEdges() {
  edgeList.innerHTML = "";
  state.graph.edges.forEach((edge) => {
    const fromNode = state.graph.nodes.find((n) => n.id === edge.fromNodeId);
    const toNode = state.graph.nodes.find((n) => n.id === edge.toNodeId);
    const fromPort = fromNode?.ports.find((p) => p.id === edge.fromPortId);
    const toPort = toNode?.ports.find((p) => p.id === edge.toPortId);
    const card = document.createElement("div");
    card.className = "edge-card";
    const color = edge.tags?.color ?? "#2e6cdf";
    card.innerHTML = `
      <div><strong>${fromNode?.name ?? "?"}</strong> (${fromPort?.name ?? "?"}) -> <strong>${toNode?.name ?? "?"}</strong> (${toPort?.name ?? "?"})</div>
      <div class="edge-row">
        <div style="font-size:12px;color:${color}">Color: ${color}</div>
        <input type="color" value="${color}" data-edge="${edge.id}" />
      </div>
    `;
    const picker = card.querySelector(`input[data-edge="${edge.id}"]`);
    picker?.addEventListener("input", (event) => {
      const newColor = event.target.value;
      edge.tags = edge.tags ?? {};
      edge.tags.color = newColor;
      render();
    });
    edgeList.appendChild(card);
  });
}

function renderPorts() {
  if (!selectedNodeLabel || !portList) return;
  portList.innerHTML = "";
  const node = state.graph.nodes.find((n) => n.id === state.selectedNodeId);
  if (!node) {
    selectedNodeLabel.textContent = "None";
    if (selectedNodeBanner) {
      if (selectedNodeValue) {
        selectedNodeValue.textContent = "None";
      } else {
        selectedNodeBanner.textContent = "Selected: None";
      }
    }
    updateConnectSummary();
    renderPropList(null);
    return;
  }
  selectedNodeLabel.textContent = `${node.name} (${node.type})`;
  if (selectedNodeBanner) {
    if (selectedNodeValue) {
      selectedNodeValue.textContent = node.name;
    } else {
      selectedNodeBanner.textContent = `Selected: ${node.name}`;
    }
  }
  node.ports.forEach((port) => {
    const chip = document.createElement("div");
    chip.className = "port-chip";
    const ref = `${node.id}:${port.id}`;
    if (state.selectedPortRef === ref) {
      chip.classList.add("selected");
    }
    chip.innerHTML = `
      <span>${port.name}${port.index ? ` (#${port.index})` : ""}</span>
      <button data-port="${ref}">Connect</button>
    `;
    chip.addEventListener("click", () => selectPort(node.id, port.id));
    chip.querySelector("button").addEventListener("click", (event) => {
      event.stopPropagation();
      selectPort(node.id, port.id);
    });
    portList.appendChild(chip);
  });

  updateConnectSummary();
  renderPropList(node);
}

function beginDrag(event, node) {
  state.drag = {
    nodeId: node.id,
    startX: event.clientX,
    startY: event.clientY,
    originX: node.x || 40,
    originY: node.y || 40,
  };
}

window.addEventListener("mousemove", (event) => {
  if (!state.drag) return;
  const node = state.graph.nodes.find((n) => n.id === state.drag.nodeId);
  if (!node) return;
  const dx = (event.clientX - state.drag.startX) / viewState.zoom;
  const dy = (event.clientY - state.drag.startY) / viewState.zoom;
  node.x = state.drag.originX + dx;
  node.y = state.drag.originY + dy;
  renderBoard();
});

board?.addEventListener("mousedown", (event) => {
  const isBackground = event.target === board || event.target === boardContent || event.target === edgeLayer;
  if (!isBackground) {
    return;
  }
  if (event.button !== 0 && event.button !== 1) {
    return;
  }
  viewState.panning = true;
  viewState.panStartX = event.clientX - viewState.offsetX;
  viewState.panStartY = event.clientY - viewState.offsetY;
  event.preventDefault();
});

window.addEventListener("mousemove", (event) => {
  if (!viewState.panning) return;
  viewState.offsetX = event.clientX - viewState.panStartX;
  viewState.offsetY = event.clientY - viewState.panStartY;
  updateZoom();
});

window.addEventListener("mouseup", () => {
  state.drag = null;
});

window.addEventListener("mouseup", () => {
  if (viewState.panning) {
    viewState.panning = false;
  }
});

function drawEdges() {
  if (!edgeLayer) {
    return;
  }
  edgeLayer.innerHTML = "";
  state.graph.edges.forEach((edge) => {
    const fromNode = state.graph.nodes.find((n) => n.id === edge.fromNodeId);
    const toNode = state.graph.nodes.find((n) => n.id === edge.toNodeId);
    if (!fromNode || !toNode) return;
    const fromEl = boardContent.querySelector(`[data-node-id="${fromNode.id}"]`);
    const toEl = boardContent.querySelector(`[data-node-id="${toNode.id}"]`);
    const fromPortEl = boardContent.querySelector(`[data-node="${fromNode.id}"][data-port="${edge.fromPortId}"]`);
    const toPortEl = boardContent.querySelector(`[data-node="${toNode.id}"][data-port="${edge.toPortId}"]`);
    if (!fromEl || !toEl) return;
    const fromRect = (fromPortEl ?? fromEl).getBoundingClientRect();
    const toRect = (toPortEl ?? toEl).getBoundingClientRect();
    const boardRect = boardContent.getBoundingClientRect();
    const x1 = fromRect.left - boardRect.left + fromRect.width / 2;
    const y1 = fromRect.top - boardRect.top + fromRect.height / 2;
    const x2 = toRect.left - boardRect.left + toRect.width / 2;
    const y2 = toRect.top - boardRect.top + toRect.height / 2;
    const line = document.createElementNS("http://www.w3.org/2000/svg", "line");
    line.setAttribute("x1", x1);
    line.setAttribute("y1", y1);
    line.setAttribute("x2", x2);
    line.setAttribute("y2", y2);
    const color = edge.tags?.color ?? "#2e6cdf";
    line.setAttribute("stroke", color);
    line.setAttribute("stroke-width", "2");
    edgeLayer.appendChild(line);
  });
}

document.getElementById("addNode")?.addEventListener("click", () => {
  const name = document.getElementById("nodeName").value.trim() || "Node";
  const type = document.getElementById("nodeType").value;
  const id = crypto.randomUUID().replace(/-/g, "");
  const node = {
    id,
    name,
    type,
    ports: [
      { id: crypto.randomUUID().replace(/-/g, ""), name: "P1", index: 1, direction: "InOut", tags: {} },
    ],
    x: 60 + state.graph.nodes.length * 20,
    y: 60 + state.graph.nodes.length * 20,
    tags: {},
  };
  state.graph.nodes.push(node);
  selectNode(node.id);
  render();
});

document.getElementById("addPort")?.addEventListener("click", () => {
  if (!portName || !portIndex) return;
  const node = state.graph.nodes.find((n) => n.id === state.selectedNodeId);
  if (!node) {
    setStatus("Select a node before adding ports.");
    return;
  }
  const name = portName.value.trim() || `P${node.ports.length + 1}`;
  const indexValue = parseInt(portIndex.value, 10);
  node.ports.push({
    id: crypto.randomUUID().replace(/-/g, ""),
    name,
    index: Number.isFinite(indexValue) ? indexValue : node.ports.length + 1,
    direction: "InOut",
    tags: {},
  });
  portName.value = "";
  portIndex.value = "";
  render();
});

addProp?.addEventListener("click", () => {
  const node = state.graph.nodes.find((n) => n.id === state.selectedNodeId);
  if (!node || !propKey) {
    setStatus("Select a node before setting properties.");
    return;
  }
  const key = propKey.value.trim();
  if (!key) {
    setStatus("Property key is required.");
    return;
  }
  const value = readPropEditorValue(key);
  node.wireVizProps = node.wireVizProps ?? {};
  node.wireVizProps[key] = value;
  renderPropList(node);
  clearPropEditor();
});

propKey?.addEventListener("input", () => {
  const node = state.graph.nodes.find((n) => n.id === state.selectedNodeId);
  if (!node) return;
  loadPropEditor(node, propKey.value.trim());
});

propValueArrayAdd?.addEventListener("click", (event) => {
  event.preventDefault();
  if (!propValueArrayItem || !propValueArrayList) return;
  const value = propValueArrayItem.value.trim();
  if (!value) return;
  const chip = document.createElement("div");
  chip.className = "port-chip";
  chip.innerHTML = `<span>${value}</span><button class="ghost-button">Remove</button>`;
  chip.querySelector("button").addEventListener("click", () => chip.remove());
  propValueArrayList.appendChild(chip);
  propValueArrayItem.value = "";
});

saveGlobals?.addEventListener("click", () => {
  state.graph.wireVizMetadata = parseJsonSafe(metaJson?.value) ?? {};
  state.graph.wireVizOptions = parseJsonSafe(optionsJson?.value) ?? {};
  state.graph.wireVizTweak = parseJsonSafe(tweakJson?.value) ?? {};
  state.graph.wireVizAdditionalBomItems = parseJsonSafe(bomJson?.value) ?? [];
  setStatus("WireViz globals saved.");
  schedulePreviewRefresh();
});

document.getElementById("clearSelection")?.addEventListener("click", () => {
  state.selectedPortRef = null;
  renderPorts();
  setStatus("Selection cleared.");
});

cancelConnectButton?.addEventListener("click", () => {
  state.selectedPortRef = null;
  updateConnectSummary();
  renderPorts();
});

themeToggle?.addEventListener("click", () => {
  const current = document.documentElement.getAttribute("data-theme") ?? "light";
  const next = current === "light" ? "dark" : "light";
  document.documentElement.setAttribute("data-theme", next);
  localStorage.setItem("ct3xx-theme", next);
  if (themeToggle) {
    themeToggle.textContent = next === "light" ? "Light" : "Dark";
  }
  setStatus(`Theme: ${next}`);
});

refreshPreview?.addEventListener("click", () => {
  refreshYamlPreview();
});

zoomOut?.addEventListener("click", () => {
  viewState.zoom = Math.max(0.2, viewState.zoom - 0.1);
  updateZoom();
});

zoomIn?.addEventListener("click", () => {
  viewState.zoom = Math.min(3, viewState.zoom + 0.1);
  updateZoom();
});

resetView?.addEventListener("click", () => {
  viewState.zoom = 1;
  viewState.offsetX = 0;
  viewState.offsetY = 0;
  updateZoom();
});

board?.addEventListener("wheel", (event) => {
  if (!event.ctrlKey) {
    return;
  }
  event.preventDefault();
  const direction = event.deltaY > 0 ? -0.1 : 0.1;
  viewState.zoom = Math.min(3, Math.max(0.2, viewState.zoom + direction));
  updateZoom();
});

document.getElementById("newGraph")?.addEventListener("click", () => {
  state.graph = {
    version: "1.0",
    metadata: { title: "Untitled Graph", sourceFormat: "blockgraph", tags: {} },
    nodes: [],
    edges: [],
  };
  render();
});

document.getElementById("saveGraph")?.addEventListener("click", () => {
  sendToHost("request-save-graph", { graph: state.graph });
});

document.getElementById("loadGraph")?.addEventListener("click", () => {
  sendToHost("request-open-graph");
});

document.getElementById("importWireViz")?.addEventListener("click", () => {
  if (window.chrome?.webview) {
    sendToHost("request-import-wireviz");
    return;
  }
  openWireVizViaApi();
});

document.getElementById("exportWireViz")?.addEventListener("click", () => {
  if (window.chrome?.webview) {
    sendToHost("request-export-wireviz", {
      graph: state.graph,
      compress: exportCompress?.checked ?? false,
      full: exportFull?.checked ?? false,
    });
    return;
  }
  exportWireVizViaApi();
});

exportSnapshot?.addEventListener("click", () => {
  const csv = buildSnapshotCsv(state.graph);
  const json = JSON.stringify(buildSnapshotJson(state.graph), null, 2);
  downloadFile("wireviz_snapshot.csv", csv, "text/csv");
  downloadFile("wireviz_snapshot.json", json, "application/json");
});

function sendToHost(type, payload = {}) {
  if (window.chrome?.webview) {
    window.chrome.webview.postMessage({ type, ...payload });
    return;
  }
  setStatus("Host bridge not available. Running in static web mode.");
  if (type === "request-save-graph") {
    downloadFile("graph.block.json", JSON.stringify(payload.graph, null, 2));
  }
}

function downloadFile(name, content, mime = "application/json") {
  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = name;
  link.click();
  URL.revokeObjectURL(url);
}

function handleHostMessage(message) {
  if (!message?.type) return;
  if (message.type === "graph-loaded") {
    state.graph = message.graph ?? message.data ?? message;
    render();
    setStatus("Graph loaded.");
  }
  if (message.type === "wireviz-imported") {
    state.graph = message.graph ?? message.data ?? message;
    render();
    setStatus("WireViz imported.");
  }
  if (message.type === "wireviz-exported") {
    const content = message.yaml ?? message.data?.content ?? message.data ?? "";
    const compress = exportCompress?.checked ?? false;
    if (compress) {
      const bytes = Uint8Array.from(atob(content), (c) => c.charCodeAt(0));
      downloadFile("wireviz.yaml.gz", bytes, "application/gzip");
      setStatus("WireViz exported (.gz).");
    } else {
      downloadFile("wireviz.yaml", content, "text/yaml");
      setStatus("WireViz exported.");
    }
  }
  if (message.type === "wireviz-preview") {
    if (yamlPreview) {
      yamlPreview.value = message.yaml ?? "";
    }
  }
  if (message.type === "status") {
    setStatus(message.text);
  }
}

function selectNode(nodeId) {
  state.selectedNodeId = nodeId;
  render();
}

function selectPort(nodeId, portId) {
  const ref = `${nodeId}:${portId}`;
  if (state.selectedPortRef === ref) {
    state.selectedPortRef = null;
    renderPorts();
    setStatus("Selection cleared.");
    return;
  }
  if (state.selectedPortRef && state.selectedPortRef !== ref) {
    const [fromNodeId, fromPortId] = state.selectedPortRef.split(":");
    const [toNodeId, toPortId] = ref.split(":");
    const fromNode = state.graph.nodes.find((n) => n.id === fromNodeId);
    const toNode = state.graph.nodes.find((n) => n.id === toNodeId);
    if (!fromNode || !toNode) {
      setStatus("Invalid connection selection.");
      state.selectedPortRef = null;
      render();
      return;
    }
    if ((fromNode.type !== "Cable" && toNode.type !== "Cable") || (fromNode.type === "Cable" && toNode.type === "Cable")) {
      setStatus("Connections must go through a Cable node.");
      state.selectedPortRef = null;
      renderPorts();
      return;
    }
    state.graph.edges.push({
      id: crypto.randomUUID().replace(/-/g, ""),
      fromNodeId,
      fromPortId,
      toNodeId,
      toPortId,
      tags: { color: edgeColorPicker?.value ?? "#2e6cdf" },
    });
    state.selectedPortRef = null;
    render();
    setStatus("Connection created.");
    updateConnectSummary();
    return;
  }
  state.selectedPortRef = ref;
  renderPorts();
  setStatus("Select another port to connect.");
  updateConnectSummary();
}

function updateConnectSummary() {
  if (!connectFromLabel || !connectToLabel) {
    return;
  }
  if (!state.selectedPortRef) {
    connectFromLabel.textContent = "-";
    connectToLabel.textContent = "-";
    return;
  }
  const [nodeId, portId] = state.selectedPortRef.split(":");
  const node = state.graph.nodes.find((n) => n.id === nodeId);
  const port = node?.ports.find((p) => p.id === portId);
  connectFromLabel.textContent = node && port ? `${node.name} - ${port.name}` : "-";
  connectToLabel.textContent = "Select next port";
}

function renderPropList(node) {
  if (!propList || !propKey || !propKeyList) return;
  propList.innerHTML = "";
  propKeyList.innerHTML = "";
  if (!node) {
    return;
  }
  const keys = node.type === "Cable" || node.type === "Bundle" ? cableKeys : connectorKeys;
  const props = node.wireVizProps ?? {};
  const allKeys = Array.from(new Set([...keys, ...Object.keys(props)])).sort();
  allKeys.forEach((key) => {
    const option = document.createElement("option");
    option.value = key;
    propKeyList.appendChild(option);
  });
  Object.entries(props).forEach(([key, value]) => {
    const row = document.createElement("div");
    row.className = "port-chip";
    const display = typeof value === "string" ? value : JSON.stringify(value);
    row.innerHTML = `
      <span>${key}: ${display}</span>
      <span>
        <button class="ghost-button" data-action="edit">Edit</button>
        <button class="ghost-button" data-action="remove">Remove</button>
      </span>
    `;
    row.querySelector('[data-action="edit"]').addEventListener("click", () => {
      propKey.value = key;
      loadPropEditor(node, key);
    });
    row.querySelector('[data-action="remove"]').addEventListener("click", () => {
      delete node.wireVizProps[key];
      renderPropList(node);
      schedulePreviewRefresh();
    });
    propList.appendChild(row);
  });
}

function loadPropEditor(node, key) {
  if (!propHint || !propValueTextWrap || !propValueBoolWrap || !propValueArrayWrap) return;
  const hint = propTypeHints[key] ?? "string";
  propHint.textContent = `Hint: ${hint}`;
  propValueTextWrap.classList.add("hidden");
  propValueBoolWrap.classList.add("hidden");
  propValueArrayWrap.classList.add("hidden");
  const value = node.wireVizProps?.[key];
  if (hint === "bool") {
    propValueBoolWrap.classList.remove("hidden");
    propValueBool.checked = Boolean(value);
  } else if (hint === "array") {
    propValueArrayWrap.classList.remove("hidden");
    propValueArrayList.innerHTML = "";
    if (Array.isArray(value)) {
      value.forEach((item) => {
        const chip = document.createElement("div");
        chip.className = "port-chip";
        chip.innerHTML = `<span>${item}</span><button class="ghost-button">Remove</button>`;
        chip.querySelector("button").addEventListener("click", () => chip.remove());
        propValueArrayList.appendChild(chip);
      });
    }
  } else {
    propValueTextWrap.classList.remove("hidden");
    propValueText.value = value ?? "";
  }
}

function clearPropEditor() {
  if (propValueText) propValueText.value = "";
  if (propValueBool) propValueBool.checked = false;
  if (propValueArrayList) propValueArrayList.innerHTML = "";
  if (propValueArrayItem) propValueArrayItem.value = "";
}

function readPropEditorValue(key) {
  const hint = propTypeHints[key] ?? "string";
  if (hint === "bool") {
    return Boolean(propValueBool?.checked);
  }
  if (hint === "array") {
    const values = [];
    if (propValueArrayList) {
      propValueArrayList.querySelectorAll(".port-chip span:first-child").forEach((span) => {
        values.push(span.textContent);
      });
    }
    return values;
  }
  if (hint === "number") {
    const val = parseFloat(propValueText?.value ?? "");
    return Number.isFinite(val) ? val : 0;
  }
  return propValueText?.value ?? "";
}

function parseJsonSafe(text) {
  if (!text || text.trim().length === 0) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function schedulePreviewRefresh() {
  if (!previewAuto?.checked) return;
  refreshYamlPreview();
}

async function refreshYamlPreview() {
  if (!yamlPreview) return;
  if (window.chrome?.webview) {
    sendToHost("request-export-wireviz-preview", { graph: state.graph });
    return;
  }
  const response = await fetch("api/wireviz/export", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      graph: state.graph,
      full: exportFull?.checked ?? false,
    }),
  });
  if (!response.ok) {
    yamlPreview.value = "Preview failed.";
    return;
  }
  const data = await response.json();
  const payload = data.data ?? data;
  yamlPreview.value = payload.content ?? payload;
}

async function gzipText(text) {
  if (!("CompressionStream" in window)) {
    setStatus("CompressionStream not available; exporting uncompressed.");
    return null;
  }
  const stream = new Blob([text]).stream().pipeThrough(new CompressionStream("gzip"));
  const response = new Response(stream);
  const buffer = await response.arrayBuffer();
  return new Uint8Array(buffer);
}

if (window.chrome?.webview) {
  window.chrome.webview.addEventListener("message", (event) => handleHostMessage(event.data));
}

initTheme();
initResizers();

async function loadSchema() {
  try {
    const response = await fetch("api/wireviz/schema");
    if (!response.ok) {
      return;
    }
    const data = await response.json();
    const payload = data.data ?? data;
    connectorKeys = payload.connectorKeys ?? [];
    cableKeys = payload.cableKeys ?? [];
    propTypeHints = payload.propertyTypeHints ?? {};
  } catch {
    // ignore
  }
}

async function openWireVizViaApi() {
  const input = document.createElement("input");
  input.type = "file";
  input.accept = ".yaml,.yml";
  input.onchange = async () => {
    const file = input.files?.[0];
    if (!file) return;
    const yaml = await file.text();
    const response = await fetch("api/wireviz/import", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ yaml }),
    });
    if (!response.ok) {
      setStatus("Import failed.");
      return;
    }
    const responseData = await response.json();
    state.graph = responseData.data ?? responseData;
    render();
    setStatus("WireViz imported.");
  };
  input.click();
}

async function exportWireVizViaApi() {
  const response = await fetch("api/wireviz/export", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      graph: state.graph,
      full: exportFull?.checked ?? false,
    }),
  });
  if (!response.ok) {
    setStatus("Export failed.");
    return;
  }
  const data = await response.json();
  const payload = data.data ?? data;
  const content = payload.content ?? payload;
  const compress = exportCompress?.checked ?? false;
  if (compress) {
    const gz = await gzipText(content);
    if (gz) {
      downloadFile("wireviz.yaml.gz", gz, "application/gzip");
      setStatus("WireViz exported (.gz).");
      return;
    }
  }
  downloadFile("wireviz.yaml", content, "text/yaml");
  setStatus("WireViz exported.");
}

function buildSnapshotJson(graph) {
  return {
    version: graph.version,
    metadata: graph.metadata,
    nodeCount: graph.nodes.length,
    edgeCount: graph.edges.length,
    nodes: graph.nodes.map((node) => ({
      id: node.id,
      name: node.name,
      type: node.type,
      ports: node.ports.map((port) => ({
        id: port.id,
        name: port.name,
        index: port.index,
      })),
      wireVizProps: node.wireVizProps ?? {},
    })),
    edges: graph.edges.map((edge) => ({
      id: edge.id,
      fromNodeId: edge.fromNodeId,
      fromPortId: edge.fromPortId,
      toNodeId: edge.toNodeId,
      toPortId: edge.toPortId,
      tags: edge.tags ?? {},
    })),
    globals: {
      metadata: graph.wireVizMetadata ?? {},
      options: graph.wireVizOptions ?? {},
      tweak: graph.wireVizTweak ?? {},
      additional_bom_items: graph.wireVizAdditionalBomItems ?? [],
    },
  };
}

function buildSnapshotCsv(graph) {
  const lines = [];
  lines.push("type,node_id,node_name,node_type,port_id,port_name,port_index,from_node,to_node,color");
  graph.nodes.forEach((node) => {
    node.ports.forEach((port) => {
      lines.push([
        "node",
        node.id,
        safeCsv(node.name),
        node.type,
        port.id,
        safeCsv(port.name),
        port.index ?? "",
        "",
        "",
        "",
      ].join(","));
    });
  });
  graph.edges.forEach((edge) => {
    lines.push([
      "edge",
      "",
      "",
      "",
      "",
      "",
      "",
      edge.fromNodeId,
      edge.toNodeId,
      (edge.tags?.color ?? ""),
    ].join(","));
  });
  return lines.join("\n");
}

function safeCsv(value) {
  if (value == null) return "";
  const text = String(value);
  if (text.includes(",") || text.includes("\"")) {
    return `"${text.replace(/\"/g, "\"\"")}"`;
  }
  return text;
}

render();

function initTheme() {
  const saved = localStorage.getItem("ct3xx-theme");
  if (saved === "light" || saved === "dark") {
    document.documentElement.setAttribute("data-theme", saved);
    if (themeToggle) {
      themeToggle.textContent = saved === "light" ? "Light" : "Dark";
    }
    return;
  }
  const prefersDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
  const theme = prefersDark ? "dark" : "light";
  document.documentElement.setAttribute("data-theme", theme);
  if (themeToggle) {
    themeToggle.textContent = theme === "light" ? "Light" : "Dark";
  }
}
loadSchema();
