const state = {
  data: {},
  section: null,
  item: null,
};

const sectionName = document.getElementById("sectionName");
const addSection = document.getElementById("addSection");
const sectionList = document.getElementById("sectionList");
const itemName = document.getElementById("itemName");
const addItem = document.getElementById("addItem");
const itemList = document.getElementById("itemList");
const itemTypeButton = document.getElementById("itemTypeButton");
const itemTypeMenu = document.getElementById("itemTypeMenu");
const propKey = document.getElementById("propKey");
const propValue = document.getElementById("propValue");
const addProp = document.getElementById("addProp");
const propList = document.getElementById("propList");
const yamlPreview = document.getElementById("yamlPreview");
const status = document.getElementById("status");
const sectionOptions = document.getElementById("sectionName");
const itemOptions = document.getElementById("itemOptions");
const propKeyOptions = document.getElementById("propKeyOptions");
const propValueOptions = document.getElementById("propValueOptions");
const suggestedProps = document.getElementById("suggestedProps");
const typeHint = document.getElementById("typeHint");
const requiredProps = document.getElementById("requiredProps");
const helpTooltip = document.getElementById("helpTooltip");
const jsonEditor = document.getElementById("jsonEditor");
const jsonEditorText = document.getElementById("jsonEditorText");
const jsonSave = document.getElementById("jsonSave");
const jsonCancel = document.getElementById("jsonCancel");
const themeToggle = document.getElementById("toggleTheme");
let helpTimer = null;
let helpTarget = null;
let lastKeyTime = 0;

document.addEventListener("keydown", () => {
  lastKeyTime = Date.now();
  hideHelp();
});

function scheduleHelp(target) {
  if (!target) return;
  if (helpTarget === target) return;
  helpTarget = target;
  if (helpTimer) clearTimeout(helpTimer);
  helpTimer = setTimeout(() => {
    if (!helpTarget) return;
    if (Date.now() - lastKeyTime < 2000) return;
    showHelp(helpTarget);
  }, 2000);
}

function clearHelp(target) {
  if (!target || target !== helpTarget) return;
  if (helpTimer) clearTimeout(helpTimer);
  hideHelp();
}

document.addEventListener("mouseover", (event) => {
  const target = event.target?.closest?.("[data-help]");
  if (!target) return;
  scheduleHelp(target);
});

document.addEventListener("mouseout", (event) => {
  const target = event.target?.closest?.("[data-help]");
  if (!target) return;
  clearHelp(target);
});

const schemaState = {
  topLevelKeys: [],
  sectionKeys: {},
  elementTypes: [],
  elementTypeKeys: {},
  propertyValueHints: {},
  requiredElementTypeKeys: {},
  elementTypeHelp: {},
  freeFormElementTypes: [],
  genericElementOptionalKeys: {},
  elementFieldTemplates: {},
};

const fallbackElementTypes = [
  "relay",
  "resistor",
  "inductor",
  "transformer",
  "current_transformer",
  "limit",
  "assembly",
  "tester_supply",
  "tester_output",
  "testsystem",
  "switch",
  "fuse",
  "diode",
  "load",
  "voltage_divider",
  "sensor",
  "opto",
  "transistor",
];

document.getElementById("newDoc").addEventListener("click", () => {
  state.data = {};
  state.section = null;
  state.item = null;
  render();
});

document.getElementById("loadYaml").addEventListener("click", async () => {
  const input = document.createElement("input");
  input.type = "file";
  input.accept = ".yaml,.yml";
  input.onchange = async () => {
    const file = input.files?.[0];
    if (!file) return;
    const text = await file.text();
    const response = await fetch("api/simulation/import", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ yaml: text }),
    });
    const data = await response.json();
    if (!data?.success) {
      status.textContent = data?.error?.message ?? "Import failed.";
      return;
    }
    state.data = normalizeLoadedData(data?.data ?? {});
    state.section = null;
    state.item = null;
    render();
    await validateYaml(text);
  };
  input.click();
});

document.getElementById("loadJson").addEventListener("click", async () => {
  const input = document.createElement("input");
  input.type = "file";
  input.accept = ".json";
  input.onchange = async () => {
    const file = input.files?.[0];
    if (!file) return;
    const text = await file.text();
    state.data = normalizeLoadedData(JSON.parse(text));
    state.section = null;
    state.item = null;
    render();
  };
  input.click();
});

document.getElementById("saveJson").addEventListener("click", () => {
  const blob = new Blob([JSON.stringify(state.data, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "simulation.json";
  link.click();
  URL.revokeObjectURL(url);
});

document.getElementById("exportYaml").addEventListener("click", async () => {
  const response = await fetch("api/simulation/export", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ data: state.data }),
  });
  const data = await response.json();
  yamlPreview.value = data?.data?.content ?? "";
});

themeToggle?.addEventListener("click", () => {
  const current = document.documentElement.getAttribute("data-theme") ?? "light";
  const next = current === "light" ? "dark" : "light";
  document.documentElement.setAttribute("data-theme", next);
  localStorage.setItem("ct3xx-sim-theme", next);
  if (themeToggle) {
    themeToggle.textContent = next === "light" ? "Light" : "Dark";
  }
});

addSection.addEventListener("click", () => {
  const name = sectionName.value.trim();
  if (!name) return;
  state.data[name] = state.data[name] ?? {};
  state.section = name;
  state.item = null;
  render();
});

sectionOptions?.addEventListener("change", () => {
  const value = sectionOptions.value.trim();
  if (!value) return;
  state.section = value;
  state.item = null;
  render();
});

addItem.addEventListener("click", () => {
  if (!state.section) return;
  const name = itemName.value.trim();
  if (!name) return;
  state.data[state.section][name] = state.data[state.section][name] ?? {};
  const selectedType = itemTypeButton?.dataset?.value?.trim() ?? "";
  if (state.section.toLowerCase() === "elements" && selectedType) {
    state.data[state.section][name].type = selectedType;
    state.data[state.section][name].id = name;
  }
  state.item = name;
  itemName.value = "";
  if (itemTypeButton) {
    itemTypeButton.dataset.value = "";
    itemTypeButton.textContent = "Select type";
  }
  render();
});

addProp.addEventListener("click", () => {
  if (!state.section || !state.item) return;
  const key = propKey.value.trim();
  if (!key) return;
  if (state.section.toLowerCase() === "elements" && key.toLowerCase() === "type") {
    const current = state.data?.[state.section]?.[state.item]?.type;
    if (current) {
      status.textContent = "Type is locked and cannot be changed.";
      propKey.value = "";
      propValue.value = "";
      return;
    }
  }
  const valueText = propValue.value.trim();
  let value = valueText;
  try {
    value = JSON.parse(valueText);
  } catch {
    value = valueText;
  }
  state.data[state.section][state.item][key] = value;
  propKey.value = "";
  propValue.value = "";
  render();
});

function render() {
  renderSections();
  ensureDefaultSection();
  renderItems();
  renderProps();
  updatePreview();
  updateSectionOptions();
}

function renderSections() {
  sectionList.innerHTML = "";
  updateSectionOptions();
  Object.keys(state.data).forEach((key) => {
    const chip = document.createElement("div");
    chip.className = "chip";
    chip.innerHTML = `<span>${key}</span><div class="chip-actions"><button class="chip-open">Open</button><button class="chip-delete">Delete</button></div>`;
    chip.querySelector(".chip-open").addEventListener("click", () => {
      state.section = key;
      state.item = null;
      render();
    });
    chip.querySelector(".chip-delete").addEventListener("click", () => {
      delete state.data[key];
      if (state.section === key) {
        state.section = null;
        state.item = null;
      }
      render();
    });
    sectionList.appendChild(chip);
  });
}

function ensureDefaultSection() {
  if (state.section) return;
  const available = schemaState.topLevelKeys.length > 0 ? schemaState.topLevelKeys : ["elements"];
  if (available.length === 1) {
    state.section = available[0];
  }
}

function renderItems() {
  itemList.innerHTML = "";
  updateItemOptions();
  updateItemTypeOptions();
  if (!state.section) return;
  const items = state.data[state.section] ?? {};
  Object.keys(items).forEach((key) => {
    const chip = document.createElement("div");
    chip.className = "chip";
    chip.innerHTML = `<span>${key}</span><div class="chip-actions"><button class="chip-open">Open</button><button class="chip-delete">Delete</button></div>`;
    chip.querySelector(".chip-open").addEventListener("click", () => {
      state.item = key;
      render();
    });
    chip.querySelector(".chip-delete").addEventListener("click", () => {
      delete state.data[state.section][key];
      if (state.item === key) {
        state.item = null;
      }
      render();
    });
    itemList.appendChild(chip);
  });
}

function renderProps() {
  propList.innerHTML = "";
  updatePropKeyOptions();
  updatePropValueOptions();
  updateSuggestedProps();
  updateRequiredFields();
  if (!state.section || !state.item) return;
  const props = state.data[state.section][state.item] ?? {};
  const element = state.data?.[state.section]?.[state.item] ?? {};
  const typeValue = (element.type ?? element.Type ?? "").toString().toLowerCase();
  const requiredKeys = new Set(schemaState.requiredElementTypeKeys?.[typeValue] ?? []);
  Object.entries(props).forEach(([key, value]) => {
    const chip = document.createElement("div");
    chip.className = "chip";
    const isLocked = ["id", "type"].includes(key.toLowerCase()) || requiredKeys.has(key);
    const preview = isObjectValue(value) ? "[object]" : JSON.stringify(value);
    chip.innerHTML = isLocked
      ? `<span>${key}: ${preview}</span>`
      : `<span>${key}: ${preview}</span><div class="chip-actions"><button class="chip-delete">Delete</button></div>`;
    if (shouldUseJsonEditor(value, key)) {
      const edit = document.createElement("button");
      edit.className = "chip-edit";
      edit.textContent = "Edit";
      edit.addEventListener("click", () => {
        openJsonEditor(value, (next) => {
          state.data[state.section][state.item][key] = next;
          render();
        });
      });
      const actions = chip.querySelector(".chip-actions") ?? chip;
      actions.appendChild(edit);
    } else {
      const edit = document.createElement("button");
      edit.className = "chip-edit";
      edit.textContent = "Edit";
      edit.addEventListener("click", () => {
        propKey.value = key;
        propValue.value = typeof value === "string" ? value : JSON.stringify(value);
        propValue.focus();
      });
      const actions = chip.querySelector(".chip-actions") ?? chip;
      actions.appendChild(edit);
    }
    if (!isLocked) {
      chip.querySelector(".chip-delete").addEventListener("click", () => {
        delete state.data[state.section][state.item][key];
        render();
      });
    }
    propList.appendChild(chip);
  });
}

async function updatePreview() {
  if (!yamlPreview) return;
  const response = await fetch("api/simulation/export", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ data: state.data }),
  });
  const data = await response.json();
  const content = data?.data?.content;
  yamlPreview.value = typeof content === "string" ? content : JSON.stringify(content ?? "");
  status.textContent = "Preview updated.";
}

async function loadSchema() {
  try {
    const response = await fetch("api/simulation/schema");
    const data = await response.json();
    if (!data?.success) {
      status.textContent = data?.error?.message ?? "Schema fetch failed.";
      return;
    }
    schemaState.topLevelKeys = normalizeArray(getSchemaField(data, "topLevelKeys"));
    schemaState.sectionKeys = getSchemaField(data, "sectionKeys") ?? {};
    schemaState.elementTypes = normalizeArray(getSchemaField(data, "elementTypes"));
    if (schemaState.elementTypes.length <= 1) {
      schemaState.elementTypes = fallbackElementTypes.slice();
    }
    schemaState.elementTypeKeys = getSchemaField(data, "elementTypeKeys") ?? {};
    schemaState.propertyValueHints = getSchemaField(data, "propertyValueHints") ?? {};
    schemaState.requiredElementTypeKeys = getSchemaField(data, "requiredElementTypeKeys") ?? {};
    schemaState.elementTypeHelp = getSchemaField(data, "elementTypeHelp") ?? {};
    schemaState.freeFormElementTypes = normalizeArray(getSchemaField(data, "freeFormElementTypes"));
    schemaState.genericElementOptionalKeys = getSchemaField(data, "genericElementOptionalKeys") ?? {};
    schemaState.elementFieldTemplates = getSchemaField(data, "elementFieldTemplates") ?? {};
    updateSectionOptions();
    updatePropKeyOptions();
    updatePropValueOptions();
    render();
  } catch {
    status.textContent = "Schema fetch failed.";
  }
}

async function validateYaml(yaml) {
  try {
    const response = await fetch("api/simulation/parse", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ yaml }),
    });
    const data = await response.json();
    if (!data?.success) {
      status.textContent = data?.error?.message ?? "Parser failed.";
      return;
    }
    const count = data?.data?.elements?.length ?? 0;
    status.textContent = `Parser ok. ${count} element(s).`;
  } catch {
    status.textContent = "Parser failed.";
  }
}

function updateSectionOptions() {
  if (!sectionOptions) return;
  sectionOptions.innerHTML = "";
  const keys = schemaState.topLevelKeys.length > 0 ? schemaState.topLevelKeys : ["elements"];
  keys.forEach((key) => {
    const option = document.createElement("option");
    option.value = key;
    option.textContent = key;
    sectionOptions.appendChild(option);
  });
  if (keys.length === 0) {
    const option = document.createElement("option");
    option.value = "elements";
    option.textContent = "elements";
    sectionOptions.appendChild(option);
  }
  if (state.section && Array.from(sectionOptions.options).some((o) => o.value === state.section)) {
    sectionOptions.value = state.section;
  } else if (sectionOptions.options.length > 0) {
    sectionOptions.value = sectionOptions.options[0].value;
  }
}

function updatePropKeyOptions() {
  if (!propKeyOptions) return;
  propKeyOptions.innerHTML = "";
  if (!state.section) return;
  let keys = schemaState.sectionKeys?.[state.section] ?? [];
  if (state.section.toLowerCase() === "elements" && state.item) {
    const element = state.data?.[state.section]?.[state.item] ?? {};
    const typeValue = (element.type ?? element.Type ?? "").toString().toLowerCase();
    const typeKeys = schemaState.elementTypeKeys?.[typeValue] ?? [];
    keys = Array.from(new Set([...keys, ...typeKeys]));
    if (element.type) {
      keys = keys.filter((key) => key.toLowerCase() !== "type");
    }
  }
  keys.forEach((key) => {
    const option = document.createElement("option");
    option.value = key;
    propKeyOptions.appendChild(option);
  });
}

function updateItemOptions() {
  if (!itemOptions) return;
  itemOptions.innerHTML = "";
  const section = state.section ?? (schemaState.topLevelKeys[0] ?? "elements");
  const items = state.data[section] ?? {};
  Object.keys(items).forEach((key) => {
    const option = document.createElement("option");
    option.value = key;
    itemOptions.appendChild(option);
  });

  if (section.toLowerCase() === "elements") {
    schemaState.elementTypes.forEach((value) => {
      const option = document.createElement("option");
      option.value = `${value}_1`;
      itemOptions.appendChild(option);
    });
  }
}

function updateItemTypeOptions() {
  if (!itemTypeButton || !itemTypeMenu) return;
  itemTypeMenu.innerHTML = "";
  const types = schemaState.elementTypes.length > 1 ? schemaState.elementTypes : fallbackElementTypes;
  status.textContent = `Types loaded: ${types.length}`;
  types.forEach((value) => {
    const option = document.createElement("div");
    option.className = "dropdown-option";
    option.textContent = value;
    option.setAttribute("data-help", resolveHelpText(value) || `Type: ${value}`);
    option.addEventListener("mouseenter", () => scheduleHelp(option));
    option.addEventListener("mouseleave", () => clearHelp(option));
    option.addEventListener("click", () => {
      itemTypeButton.dataset.value = value;
      itemTypeButton.textContent = value;
      itemTypeMenu.classList.remove("open");
    });
    itemTypeMenu.appendChild(option);
  });

  if (state.section?.toLowerCase() === "elements" && state.item) {
    const element = state.data?.[state.section]?.[state.item] ?? {};
    const typeValue = (element.type ?? element.Type ?? "").toString();
    if (typeValue) {
      itemTypeButton.dataset.value = typeValue;
      itemTypeButton.textContent = typeValue;
      itemTypeButton.setAttribute("data-help", resolveHelpText(typeValue));
      return;
    }
  }

  itemTypeButton.setAttribute("data-help", "Select the element type. Type is locked after creation.");
}

itemTypeButton?.addEventListener("click", () => {
  if (itemTypeButton.classList.contains("disabled")) return;
  itemTypeMenu?.classList.toggle("open");
});

document.addEventListener("click", (event) => {
  if (!itemTypeMenu || !itemTypeButton) return;
  const within = event.target?.closest?.("#itemTypeDropdown");
  if (!within) {
    itemTypeMenu.classList.remove("open");
  }
});

function resolveHelpText(typeValue) {
  const entries = schemaState.elementTypeHelp?.[typeValue] ?? {};
  const lang = (navigator.language || "en").toLowerCase();
  if (lang.startsWith("de")) {
    return entries.de ?? entries.en ?? "";
  }
  return entries.en ?? entries.de ?? "";
}

document.addEventListener("mousemove", (event) => {
  const target = event.target?.closest?.("[data-help]");
  if (helpTooltip?.classList.contains("visible") && target) {
    const rect = target.getBoundingClientRect();
    const top = Math.max(12, rect.top - 8);
    const left = Math.min(window.innerWidth - 340, rect.left);
    helpTooltip.style.top = `${top}px`;
    helpTooltip.style.left = `${left}px`;
  }
});

function getSchemaField(payload, name) {
  const data = payload?.data ?? {};
  return data[name] ?? data[name[0].toUpperCase() + name.slice(1)];
}

function normalizeArray(value) {
  if (Array.isArray(value)) return value;
  if (value && Array.isArray(value.$values)) return value.$values;
  if (value && typeof value === "object") return Object.values(value);
  return [];
}

function normalizeLoadedData(data) {
  if (!data || typeof data !== "object") return {};
  const clone = JSON.parse(JSON.stringify(data));
  if (Array.isArray(clone.elements)) {
    const mapped = {};
    clone.elements.forEach((element, index) => {
      const name = element?.id ?? `${index}`;
      mapped[name] = element ?? {};
    });
    clone.elements = mapped;
  }
  return clone;
}

function isObjectValue(value) {
  return value && typeof value === "object";
}

function isStructuredKey(key) {
  if (!key) return false;
  const lowered = key.toLowerCase();
  return lowered.includes("coil") ||
    lowered.includes("contacts") ||
    lowered.includes("ports") ||
    lowered.includes("nodes") ||
    lowered.includes("metadata") ||
    key.includes(".");
}

function shouldUseJsonEditor(value, key) {
  if (isObjectValue(value)) return true;
  return isStructuredKey(key);
}

function getTemplateValue(typeValue, keyPath) {
  const typeTemplates = schemaState.elementFieldTemplates?.[typeValue];
  if (!typeTemplates) return null;
  const rootKey = keyPath.split(".")[0].trim();
  return typeTemplates[rootKey] ?? null;
}

function openJsonEditor(value, onSave) {
  if (!jsonEditor || !jsonEditorText) return;
  jsonEditorText.value = JSON.stringify(value, null, 2);
  jsonEditor.classList.add("open");
  jsonEditor.setAttribute("aria-hidden", "false");
  jsonSave.onclick = () => {
    try {
      const parsed = JSON.parse(jsonEditorText.value);
      onSave(parsed);
      closeJsonEditor();
    } catch {
      status.textContent = "Invalid JSON.";
    }
  };
  jsonCancel.onclick = () => closeJsonEditor();
}

function closeJsonEditor() {
  jsonEditor.classList.remove("open");
  jsonEditor.setAttribute("aria-hidden", "true");
  jsonSave.onclick = null;
}


function updateSuggestedProps() {
  if (!suggestedProps) return;
  suggestedProps.innerHTML = "";
  typeHint.textContent = "";
  if (!state.section || !state.item) return;

  if (state.section.toLowerCase() !== "elements") {
    return;
  }

  const element = state.data?.[state.section]?.[state.item] ?? {};
  const typeValue = (element.type ?? element.Type ?? "").toString().toLowerCase();
  if (!typeValue) {
    typeHint.textContent = "Select type to see suggestions";
    return;
  }

  typeHint.textContent = typeValue;
  let keys = schemaState.elementTypeKeys?.[typeValue] ?? [];
  const required = new Set(schemaState.requiredElementTypeKeys?.[typeValue] ?? []);
  const isFreeForm = schemaState.freeFormElementTypes?.includes?.(typeValue);
  if (isFreeForm) {
    const genericKeys = schemaState.genericElementOptionalKeys?.[typeValue] ?? [];
    keys = Array.from(new Set([...keys, ...genericKeys]));
    const chip = document.createElement("button");
    chip.type = "button";
    chip.className = "suggested-chip";
    chip.textContent = "custom (any key)";
    chip.setAttribute("data-help", "Generic element: any additional key is allowed.");
    chip.addEventListener("click", () => {
      propKey.value = "";
      propValue.value = "";
      propKey.placeholder = "custom_key";
      propKey.focus();
    });
    suggestedProps.appendChild(chip);
  }
  keys.forEach((key) => {
    if (required.has(key)) {
      return;
    }
    const chip = document.createElement("button");
    chip.type = "button";
    chip.className = "suggested-chip";
    chip.textContent = key;
    chip.addEventListener("click", () => {
      propKey.value = key;
      updatePropValueOptions();
      propValue.focus();
    });
    suggestedProps.appendChild(chip);
  });
}

function updateRequiredFields() {
  if (!requiredProps) return;
  requiredProps.innerHTML = "";
  if (!state.section || !state.item) return;
  if (state.section.toLowerCase() !== "elements") return;

  const element = state.data?.[state.section]?.[state.item] ?? {};
  const typeValue = (element.type ?? element.Type ?? "").toString().toLowerCase();
  if (!typeValue) return;

  const keys = schemaState.requiredElementTypeKeys?.[typeValue] ?? [];
  keys.forEach((key) => {
    const wrapper = document.createElement("div");
    wrapper.className = "required-field";
    const label = document.createElement("label");
    label.textContent = key;
    let currentValue = getNestedValue(element, key);
    if (currentValue === undefined || currentValue === null || currentValue === "") {
      currentValue = getTemplateValue(typeValue, key) ?? "";
    }
    if (shouldUseJsonEditor(currentValue, key)) {
      const button = document.createElement("button");
      button.type = "button";
      button.textContent = "Edit JSON";
      button.addEventListener("click", () => {
        openJsonEditor(currentValue, (next) => {
          const target = state.data[state.section][state.item];
          setNestedValue(target, key, next);
          render();
        });
      });
      wrapper.appendChild(label);
      wrapper.appendChild(button);
    } else {
      const input = document.createElement("input");
      input.value = currentValue ?? "";
      input.placeholder = key.includes("|") ? "one of these" : "required";
      if (key.toLowerCase() === "type" && element.type) {
        input.disabled = true;
      }
      input.addEventListener("change", () => {
        if (!state.section || !state.item) return;
        const target = state.data[state.section][state.item];
        if (key.toLowerCase() === "type" && target.type) {
          input.value = target.type;
          status.textContent = "Type is locked and cannot be changed.";
          return;
        }
        const value = parseInputValue(input.value);
        setNestedValue(target, key, value);
        render();
      });
      wrapper.appendChild(label);
      wrapper.appendChild(input);
    }
    requiredProps.appendChild(wrapper);
  });
}

function parseInputValue(text) {
  const trimmed = (text ?? "").trim();
  if (!trimmed) return "";
  try {
    return JSON.parse(trimmed);
  } catch {
    return trimmed;
  }
}

function setNestedValue(target, keyPath, value) {
  const first = keyPath.split("|")[0].trim();
  const parts = first.split(".").map((part) => part.trim()).filter(Boolean);
  if (parts.length === 0) return;
  let current = target;
  for (let i = 0; i < parts.length - 1; i += 1) {
    const part = parts[i];
    if (!current[part] || typeof current[part] !== "object") {
      current[part] = {};
    }
    current = current[part];
  }
  current[parts[parts.length - 1]] = value;
}

function getNestedValue(target, keyPath) {
  const first = keyPath.split("|")[0].trim();
  const parts = first.split(".").map((part) => part.trim()).filter(Boolean);
  if (parts.length === 0) return "";
  let current = target;
  for (let i = 0; i < parts.length; i += 1) {
    if (!current || typeof current !== "object") return "";
    current = current[parts[i]];
  }
  return current ?? "";
}

function updatePropValueOptions() {
  if (!propValueOptions) return;
  propValueOptions.innerHTML = "";
  const key = propKey.value.trim().toLowerCase();
  if (key === "type") {
    schemaState.elementTypes.forEach((value) => {
      const option = document.createElement("option");
      option.value = value;
      propValueOptions.appendChild(option);
    });
    return;
  }

  const hinted = schemaState.propertyValueHints?.[key] ?? [];
  hinted.forEach((value) => {
    const option = document.createElement("option");
    option.value = value;
    propValueOptions.appendChild(option);
  });
}

itemName.addEventListener("input", () => {
  if (!state.section) return;
  if (state.section.toLowerCase() !== "elements") return;
  const key = (propKey.value || "type").trim();
  if (key.toLowerCase() !== "type") return;
  updatePropValueOptions();
  updateItemTypeOptions();
});

function showHelp(target) {
  if (!helpTooltip || !target) return;
  const text = target.getAttribute("data-help");
  if (!text) return;
  helpTooltip.textContent = text;
  helpTooltip.setAttribute("aria-hidden", "false");
  helpTooltip.classList.add("visible");
  const rect = target.getBoundingClientRect();
  const top = Math.max(12, rect.top - 8);
  const left = Math.min(window.innerWidth - 340, rect.left);
  helpTooltip.style.top = `${top}px`;
  helpTooltip.style.left = `${left}px`;
}

function hideHelp() {
  if (!helpTooltip) return;
  helpTooltip.classList.remove("visible");
  helpTooltip.setAttribute("aria-hidden", "true");
  helpTarget = null;
}

render();
loadSchema();

initTheme();

function initTheme() {
  const saved = localStorage.getItem("ct3xx-sim-theme");
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
