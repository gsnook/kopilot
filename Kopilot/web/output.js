/* Kopilot Output — Rendering Engine
 *
 * Provides the C# <-> JS bridge for the Rendered output tab.
 * Called from MainForm.cs via WebView2.ExecuteScriptAsync().
 *
 * Public API (called from C#):
 *   appendBlock(id, kind, label, content)  - add a new block
 *   updateBlock(id, content)               - update an existing block's content (streaming)
 *   finalizeBlock(id)                      - mark a block as complete (triggers Mermaid)
 *   appendToolStatus(id, html)             - append status HTML to a tool/subagent block
 *   appendSection(id, sectionKind, summaryText, defaultOpen)
 *                                          - add a collapsible <details> section
 *                                            (sectionKind: "reasoning" | "tools")
 *   setSectionContent(id, content, isMarkdown)
 *                                          - replace the section body. If
 *                                            isMarkdown is true, content is
 *                                            run through marked first.
 *   appendSectionLine(sectionId, lineId, html)
 *                                          - append a tool line (<div class="kp-tool-line">)
 *                                            inside a tool group section's body
 *   updateSectionLine(lineId, html)        - replace an existing tool line's innerHTML
 *   markSectionLineFailed(lineId)          - add the failure styling to a tool line
 *   closeSection(id, summaryText, collapse, hasFailure)
 *                                          - rewrite the summary and (optionally) collapse
 *                                            and clear the streaming pulse
 *   appendThinking(id)                     - emit the animated "Thinking..." pill
 *   removeThinking(id)                     - remove the thinking pill
 *   clearAll()                             - clear all output
 */

(function () {
	"use strict";

	var outputEl = document.getElementById("output");
	var blocks = {};
	var sections = {};   // id -> { element, bodyEl, kind }
	var lines    = {};   // lineId -> element
	var thinking = {};   // id -> element
	var mermaidLoaded = false;
	var mermaidLoading = false;
	var pendingMermaid = [];

	// Debounce timer for streaming renders
	var renderTimers = {};
	var RENDER_DEBOUNCE_MS = 40;

	// Hide broken thumbnails (e.g. when the model mentions a path that
	// doesn't exist yet). 'error' doesn't bubble, so listen in capture phase.
	document.addEventListener("error", function (e) {
		var t = e.target;
		if (t && t.tagName === "IMG" && t.closest && t.closest(".block-assistant")) {
			t.classList.add("kp-img-broken");
		}
	}, true);

	// ── Marked configuration ───────────────────────────────────────

	marked.setOptions({
		breaks: true,
		gfm: true,
		highlight: function (code, lang) {
			if (lang && hljs.getLanguage(lang)) {
				try { return hljs.highlight(code, { language: lang }).value; }
				catch (_) { /* fall through */ }
			}
			try { return hljs.highlightAuto(code).value; }
			catch (_) { /* fall through */ }
			return code;
		}
	});

	// ── Mermaid lazy loader ────────────────────────────────────────

	function loadMermaid(callback) {
		if (mermaidLoaded) { callback(); return; }
		if (mermaidLoading) { pendingMermaid.push(callback); return; }
		mermaidLoading = true;
		var script = document.createElement("script");
		script.src = "mermaid.min.js";
		script.onload = function () {
			mermaid.initialize({
				startOnLoad: false,
				theme: "dark",
				themeVariables: {
					darkMode: true,
					background: "#0d0d0d",
					primaryColor: "#3c70a0",
					primaryTextColor: "#d2d2d2",
					lineColor: "#555"
				}
			});
			mermaidLoaded = true;
			mermaidLoading = false;
			callback();
			for (var i = 0; i < pendingMermaid.length; i++) {
				pendingMermaid[i]();
			}
			pendingMermaid = [];
		};
		script.onerror = function () {
			mermaidLoading = false;
			console.warn("Failed to load mermaid.min.js");
		};
		document.head.appendChild(script);
	}

	// ── Rendering helpers ──────────────────────────────────────────

	function renderMarkdown(content) {
		return marked.parse(content || "");
	}

	function processMermaidBlocks(containerEl) {
		var codeBlocks = containerEl.querySelectorAll("pre code.language-mermaid");
		if (codeBlocks.length === 0) return;

		loadMermaid(function () {
			for (var i = 0; i < codeBlocks.length; i++) {
				var codeEl = codeBlocks[i];
				var preEl = codeEl.parentElement;
				var source = codeEl.textContent;

				var container = document.createElement("div");
				container.className = "mermaid-container";
				container.textContent = source;
				preEl.replaceWith(container);
			}
			// Use mermaid.run() to render all .mermaid-container elements
			var nodes = containerEl.querySelectorAll(".mermaid-container");
			try {
				mermaid.run({ nodes: nodes });
			} catch (e) {
				console.warn("Mermaid render error:", e);
			}
			// Wrap each rendered diagram in a pan/zoom viewport.
			// Mermaid renders asynchronously; defer to next tick so the SVG exists.
			setTimeout(function () {
				for (var j = 0; j < nodes.length; j++) {
					makeZoomable(nodes[j], "mermaid");
				}
			}, 0);
		});
	}

	function processOversizedBlocks(containerEl) {
		var MAX_H = 480;
		// <pre> code blocks
		var pres = containerEl.querySelectorAll("pre");
		for (var i = 0; i < pres.length; i++) {
			var pre = pres[i];
			if (pre.parentElement && pre.parentElement.classList.contains("kp-zoom-wrap")) continue;
			if (pre.scrollWidth > pre.clientWidth + 1 || pre.scrollHeight > MAX_H) {
				makeZoomable(pre, "pre");
			}
		}
		// Tables
		var tables = containerEl.querySelectorAll("table");
		for (var k = 0; k < tables.length; k++) {
			var tbl = tables[k];
			if (tbl.parentElement && tbl.parentElement.classList.contains("kp-zoom-wrap")) continue;
			if (tbl.scrollWidth > tbl.clientWidth + 1 || tbl.offsetHeight > MAX_H) {
				makeZoomable(tbl, "table");
			}
		}
	}

	// ── Pan / Zoom wrapper ─────────────────────────────────────────

	function makeZoomable(targetEl, kind) {
		if (!targetEl || !targetEl.parentElement) return;
		if (targetEl.parentElement.classList.contains("kp-zoom-wrap")) return;

		var wrap = document.createElement("div");
		wrap.className = "kp-zoom-wrap kp-zoom-" + kind;

		var viewport = document.createElement("div");
		viewport.className = "kp-zoom-viewport";

		var inner = document.createElement("div");
		inner.className = "kp-zoom-inner";

		targetEl.parentElement.insertBefore(wrap, targetEl);
		inner.appendChild(targetEl);
		viewport.appendChild(inner);
		wrap.appendChild(viewport);

		// Toolbar
		var toolbar = document.createElement("div");
		toolbar.className = "kp-zoom-toolbar";
		toolbar.innerHTML =
			'<button type="button" class="kp-zb" data-act="in"  title="Zoom in">+</button>' +
			'<button type="button" class="kp-zb" data-act="out" title="Zoom out">-</button>' +
			'<button type="button" class="kp-zb" data-act="fit" title="Reset">Fit</button>' +
			'<button type="button" class="kp-zb" data-act="full" title="Fullscreen">Full</button>';
		wrap.appendChild(toolbar);

		var state = { scale: 1, tx: 0, ty: 0 };

		function apply() {
			inner.style.transform =
				"translate(" + state.tx + "px," + state.ty + "px) scale(" + state.scale + ")";
		}

		function reset() {
			state.scale = 1;
			state.tx = 0;
			state.ty = 0;
			apply();
		}

		function zoomAt(factor, cx, cy) {
			var newScale = Math.max(0.1, Math.min(8, state.scale * factor));
			var k = newScale / state.scale;
			// Keep the point under (cx, cy) stationary in viewport coords.
			state.tx = cx - k * (cx - state.tx);
			state.ty = cy - k * (cy - state.ty);
			state.scale = newScale;
			apply();
		}

		viewport.addEventListener("wheel", function (e) {
			e.preventDefault();
			var rect = viewport.getBoundingClientRect();
			var cx = e.clientX - rect.left;
			var cy = e.clientY - rect.top;
			var factor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
			zoomAt(factor, cx, cy);
		}, { passive: false });

		// Drag-to-pan
		var dragging = false, lastX = 0, lastY = 0;
		viewport.addEventListener("mousedown", function (e) {
			if (e.button !== 0) return;
			dragging = true;
			lastX = e.clientX;
			lastY = e.clientY;
			viewport.classList.add("kp-zoom-dragging");
			e.preventDefault();
		});
		window.addEventListener("mousemove", function (e) {
			if (!dragging) return;
			state.tx += e.clientX - lastX;
			state.ty += e.clientY - lastY;
			lastX = e.clientX;
			lastY = e.clientY;
			apply();
		});
		window.addEventListener("mouseup", function () {
			if (!dragging) return;
			dragging = false;
			viewport.classList.remove("kp-zoom-dragging");
		});

		// Double-click resets
		viewport.addEventListener("dblclick", function (e) {
			e.preventDefault();
			reset();
		});

		// Toolbar handlers
		toolbar.addEventListener("click", function (e) {
			var btn = e.target.closest(".kp-zb");
			if (!btn) return;
			e.stopPropagation();
			var act = btn.getAttribute("data-act");
			var rect = viewport.getBoundingClientRect();
			var cx = rect.width / 2;
			var cy = rect.height / 2;
			if (act === "in") zoomAt(1.25, cx, cy);
			else if (act === "out") zoomAt(1 / 1.25, cx, cy);
			else if (act === "fit") reset();
			else if (act === "full") toggleFullscreen(wrap, reset);
		});
	}

	function toggleFullscreen(wrap, resetFn) {
		var isFull = wrap.classList.toggle("kp-zoom-full");
		if (isFull) {
			document.body.classList.add("kp-zoom-body-locked");
			if (!wrap._kpEsc) {
				wrap._kpEsc = function (e) {
					if (e.key === "Escape") toggleFullscreen(wrap, resetFn);
				};
				document.addEventListener("keydown", wrap._kpEsc);
			}
		} else {
			document.body.classList.remove("kp-zoom-body-locked");
			if (wrap._kpEsc) {
				document.removeEventListener("keydown", wrap._kpEsc);
				wrap._kpEsc = null;
			}
		}
		if (resetFn) resetFn();
	}

	function scrollToBottom() {
		// Auto-scroll only when the user is already pinned to (or near) the
		// bottom of the transcript. If they have scrolled up to read history,
		// don't yank them back down. The 60 px slop covers minor jitter in the
		// document height that arises during streaming markdown re-renders.
		var slop = 60;
		var pinned = (window.innerHeight + window.scrollY)
		           >= (document.body.scrollHeight - slop);
		if (pinned) {
			window.scrollTo(0, document.body.scrollHeight);
		}
	}

	function escapeHtml(str) {
		var div = document.createElement("div");
		div.textContent = str;
		return div.innerHTML;
	}

	// ── Public API ─────────────────────────────────────────────────

	/**
	 * Append a new message block.
	 * @param {string} id      - Unique block ID
	 * @param {string} kind    - CSS class suffix (user, assistant, tool, subagent, error, status, reasoning)
	 * @param {string} label   - Header label (e.g. "Assistant:", "Tool: edit_file")
	 * @param {string} content - Initial content (Markdown for assistant, plain text for others)
	 * @param {boolean} isMarkdown - Whether to render content as Markdown
	 */
	window.appendBlock = function (id, kind, label, content, isMarkdown) {
		var blockEl = document.createElement("div");
		blockEl.className = "block block-" + kind;
		blockEl.id = "block-" + id;

		var html = "";
		if (label) {
			html += '<div class="block-label">' + escapeHtml(label) + '</div>';
		}

		html += '<div class="block-content">';
		if (isMarkdown && content) {
			html += renderMarkdown(content);
		} else if (content) {
			html += escapeHtml(content);
		}
		html += '</div>';

		blockEl.innerHTML = html;
		outputEl.appendChild(blockEl);

		blocks[id] = {
			element: blockEl,
			kind: kind,
			isMarkdown: isMarkdown,
			rawContent: content || ""
		};

		scrollToBottom();
	};

	/**
	 * Update an existing block's content (used for streaming deltas).
	 * Debounced to avoid excessive re-renders during rapid streaming.
	 */
	window.updateBlock = function (id, content) {
		var block = blocks[id];
		if (!block) return;

		block.rawContent = content;

		// Debounce rendering
		if (renderTimers[id]) {
			clearTimeout(renderTimers[id]);
		}
		renderTimers[id] = setTimeout(function () {
			delete renderTimers[id];
			var contentEl = block.element.querySelector(".block-content");
			if (!contentEl) return;

			if (block.isMarkdown) {
				contentEl.innerHTML = renderMarkdown(block.rawContent);
			} else {
				contentEl.textContent = block.rawContent;
			}
			scrollToBottom();
		}, RENDER_DEBOUNCE_MS);
	};

	/**
	 * Mark a block as complete. Triggers Mermaid rendering for assistant blocks.
	 */
	window.finalizeBlock = function (id) {
		var block = blocks[id];
		if (!block) return;

		// Flush any pending debounced render
		if (renderTimers[id]) {
			clearTimeout(renderTimers[id]);
			delete renderTimers[id];
		}

		var contentEl = block.element.querySelector(".block-content");
		if (!contentEl) return;

		if (block.isMarkdown) {
			contentEl.innerHTML = renderMarkdown(block.rawContent);
			processMermaidBlocks(contentEl);
			processOversizedBlocks(contentEl);
		}

		scrollToBottom();
	};

	/**
	 * Append raw HTML to an existing block (for tool status, sub-agent updates).
	 */
	window.appendToolStatus = function (id, html) {
		var block = blocks[id];
		if (!block) return;

		var contentEl = block.element.querySelector(".block-content");
		if (!contentEl) return;

		contentEl.insertAdjacentHTML("beforeend", html);
		scrollToBottom();
	};

	/**
	 * Clear all output blocks.
	 */
	window.clearAll = function () {
		outputEl.innerHTML = "";
		blocks = {};
		sections = {};
		lines = {};
		thinking = {};
		for (var key in renderTimers) {
			clearTimeout(renderTimers[key]);
		}
		renderTimers = {};
	};

	// ── Collapsible sections (Reasoning / Tool group) ──────────────────

	/**
	 * Append a new collapsible section block.
	 * @param {string} id          - Unique section id
	 * @param {string} sectionKind - "reasoning" | "tools"
	 * @param {string} summaryText - Initial summary line (e.g. "Reasoning...")
	 * @param {boolean} defaultOpen - If true, render with the open attribute
	 */
	window.appendSection = function (id, sectionKind, summaryText, defaultOpen) {
		var details = document.createElement("details");
		details.id = "sec-" + id;
		details.className = "kp-section kp-section-" + sectionKind + " kp-section-active";
		if (defaultOpen) details.open = true;

		var summary = document.createElement("summary");
		summary.className = "kp-section-summary";
		summary.textContent = summaryText || "";

		var body = document.createElement("div");
		body.className = "kp-section-body";

		details.appendChild(summary);
		details.appendChild(body);

		// Re-run zoom/wrap on any code blocks or Mermaid diagrams that become
		// visible when the user expands a previously-collapsed section.
		details.addEventListener("toggle", function () {
			if (details.open) {
				try {
					processOversizedBlocks(body);
					processMermaidBlocks(body);
				} catch (_) { /* tolerate; section content may be transient */ }
			}
		});

		outputEl.appendChild(details);
		sections[id] = { element: details, bodyEl: body, kind: sectionKind };
		scrollToBottom();
	};

	/**
	 * Replace a section body's content. When isMarkdown is true the supplied
	 * content is parsed by marked first; otherwise it's treated as raw HTML.
	 * Re-applies zoom-wrap if the section is currently open so oversized
	 * content (large code blocks, Mermaid diagrams) is scrollable.
	 */
	window.setSectionContent = function (id, content, isMarkdown) {
		var sec = sections[id];
		if (!sec) return;
		sec.bodyEl.innerHTML = isMarkdown ? renderMarkdown(content) : (content || "");
		if (sec.element.open) {
			try {
				processOversizedBlocks(sec.bodyEl);
				processMermaidBlocks(sec.bodyEl);
			} catch (_) { /* see toggle handler */ }
		}
		scrollToBottom();
	};

	/**
	 * Append a new tool line inside a section's body.
	 * @param {string} sectionId - Section id created via appendSection
	 * @param {string} lineId    - Unique line id (typically the tool call id)
	 * @param {string} html      - HTML content for the line (already escaped)
	 */
	window.appendSectionLine = function (sectionId, lineId, html) {
		var sec = sections[sectionId];
		if (!sec) return;
		var line = document.createElement("div");
		line.className = "kp-tool-line";
		line.id = "ln-" + lineId;
		line.innerHTML = html || "";
		sec.bodyEl.appendChild(line);
		lines[lineId] = line;
		scrollToBottom();
	};

	/**
	 * Update an existing tool line's HTML in place (used for ToolProgress
	 * and ToolComplete to keep the line a single tidy entry).
	 */
	window.updateSectionLine = function (lineId, html) {
		var line = lines[lineId];
		if (!line) return;
		line.innerHTML = html || "";
		scrollToBottom();
	};

	/**
	 * Mark a tool line as failed so it stands out when the user expands
	 * a closed group.
	 */
	window.markSectionLineFailed = function (lineId) {
		var line = lines[lineId];
		if (!line) return;
		line.classList.add("kp-tool-line-failed");
	};

	/**
	 * Close a streaming section: rewrite the summary and (optionally)
	 * collapse the body. Always removes the active-pulse class.
	 * @param {string} id          - Section id
	 * @param {string} summaryText - New summary line (e.g. "Read 3 files (8s)")
	 * @param {boolean} collapse   - If true, remove the open attribute
	 * @param {boolean} hasFailure - If true, apply failure-colour styling
	 */
	window.closeSection = function (id, summaryText, collapse, hasFailure) {
		var sec = sections[id];
		if (!sec) return;
		var summary = sec.element.querySelector(".kp-section-summary");
		if (summary) {
			summary.textContent = summaryText || "";
			if (hasFailure) summary.classList.add("kp-summary-failure");
			else            summary.classList.remove("kp-summary-failure");
		}
		sec.element.classList.remove("kp-section-active");
		if (collapse) sec.element.open = false;
		scrollToBottom();
	};

	// ── Thinking indicator ──────────────────────────────────────────

	/**
	 * Emit the animated "Thinking..." pill. Shown for the dead air between
	 * the user pressing Send and the first event of the turn.
	 */
	window.appendThinking = function (id) {
		if (thinking[id]) return;
		var el = document.createElement("div");
		el.className = "kp-thinking";
		el.id = "thk-" + id;
		el.innerHTML =
			'<span class="kp-thinking-dots">' +
				'<span></span><span></span><span></span>' +
			'</span>' +
			'<span>Thinking...</span>';
		outputEl.appendChild(el);
		thinking[id] = el;
		scrollToBottom();
	};

	/**
	 * Remove the thinking pill (called when the first event arrives).
	 */
	window.removeThinking = function (id) {
		var el = thinking[id];
		if (!el) return;
		if (el.parentElement) el.parentElement.removeChild(el);
		delete thinking[id];
	};

})();
