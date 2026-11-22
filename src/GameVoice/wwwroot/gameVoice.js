// gameVoice.js - shared speech recognition and text-to-speech helpers (window.GameVoice).

window.GameVoice = window.GameVoice || {};
window.GameVoice.version = 'gamevoice-1.0.0';

window.GameVoice.isSupported = function () {
  try {
    var hasSR = !!(window.SpeechRecognition || window.webkitSpeechRecognition);
    var hasTTS = !!(window.speechSynthesis && window.SpeechSynthesisUtterance);
    return hasSR || hasTTS;
  } catch (e) {
    return false;
  }
};

window.GameVoice.isProbablyPhone = function () {
  try {
    var ua = (navigator && navigator.userAgent) ? navigator.userAgent : '';
    ua = (ua || '').toLowerCase();

    if (ua.indexOf('android') >= 0) return true;
    if (ua.indexOf('iphone') >= 0) return true;
    if (ua.indexOf('ipad') >= 0) return true;
    if (ua.indexOf('ipod') >= 0) return true;

    try {
      if (window.matchMedia) {
        if (window.matchMedia('(hover: none)').matches) return true;
        if (window.matchMedia('(pointer: coarse)').matches) return true;
      }
    } catch (e2) {
    }

    return false;
  } catch (e) {
    return false;
  }
};

window.GameVoice.unlockTts = function () {
  try {
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) return false;

    try {
      window.speechSynthesis.resume();
    } catch (e0) {
    }

    try {
      if (window.GameVoice.__ttsUnlocked) {
        return true;
      }

      var probe = new SpeechSynthesisUtterance('');
      probe.volume = 0;
      probe.rate = 1;
      probe.pitch = 1;
      window.speechSynthesis.speak(probe);
      window.GameVoice.__ttsUnlocked = true;
      return true;
    } catch (e1) {
    }

    return true;
  } catch (e) {
    return false;
  }
};

window.GameVoice.installTtsUnlocker = function () {
  try {
    if (window.GameVoice.__ttsUnlockerInstalled) return true;
    window.GameVoice.__ttsUnlockerInstalled = true;

    var handler = function () {
      try {
        window.GameVoice.unlockTts();
      } catch (e0) {
      }
      try {
        document.removeEventListener('pointerdown', handler, true);
        document.removeEventListener('keydown', handler, true);
      } catch (e1) {
      }
    };

    // Capture phase to run as early as possible in a user gesture.
    document.addEventListener('pointerdown', handler, true);
    document.addEventListener('keydown', handler, true);
    return true;
  } catch (e) {
    return false;
  }
};

window.GameVoice._rec = null;
window.GameVoice._dotNetRef = null;
window.GameVoice._listening = false;
window.GameVoice._shouldListen = false;
window.GameVoice._bufferText = '';
window.GameVoice._latestInterim = '';
window.GameVoice._flushTimer = null;
window.GameVoice._stopFlushTimer = null;
window.GameVoice._sessionId = 0;
window.GameVoice._lastSentText = '';
window.GameVoice._lastSentAtMs = 0;

window.GameVoice._flush = function (forceInterim) {
  try {
    console.log('[GameVoice._flush] forceInterim=', forceInterim, 'bufferText=', window.GameVoice._bufferText, 'latestInterim=', window.GameVoice._latestInterim);
    if (!window.GameVoice._dotNetRef) {
      console.log('[GameVoice._flush] no dotNetRef');
      return false;
    }

    var text = (window.GameVoice._bufferText || '').trim();
    if (!text && forceInterim) {
      text = (window.GameVoice._latestInterim || '').trim();
    }

    if (!text) return false;

    // Dedupe.
    if (text === window.GameVoice._lastSentText) {
      window.GameVoice._bufferText = '';
      window.GameVoice._latestInterim = '';
      return false;
    }

    window.GameVoice._lastSentText = text;
    window.GameVoice._lastSentAtMs = Date.now();
    window.GameVoice._bufferText = '';
    window.GameVoice._latestInterim = '';

    console.log('[GameVoice._flush] sending text=', text);
    window.GameVoice._dotNetRef.invokeMethodAsync('OnVoiceTranscript', text);
    return true;
  } catch (e) {
    return false;
  }
};

window.GameVoice._scheduleFlush = function () {
  try {
    if (window.GameVoice._flushTimer) {
      clearTimeout(window.GameVoice._flushTimer);
      window.GameVoice._flushTimer = null;
    }

    // Intentionally no-op: we only transmit when push-to-talk is released.
  } catch (e) {
  }
};

window.GameVoice.start = function (dotNetRef) {
  try {
    console.log('[GameVoice.start] called, dotNetRef=', !!dotNetRef);
    var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) {
      console.log('[GameVoice.start] SpeechRecognition not supported');
      return false;
    }

    // New push-to-talk session: invalidate any pending delayed stop-flush.
    window.GameVoice._sessionId = (window.GameVoice._sessionId || 0) + 1;
    try {
      if (window.GameVoice._stopFlushTimer) {
        clearTimeout(window.GameVoice._stopFlushTimer);
        window.GameVoice._stopFlushTimer = null;
      }
    } catch (eStopTimer0) {
    }

    // Clear any prior buffered text so it cannot leak into the next transmission.
    window.GameVoice._bufferText = '';
    window.GameVoice._latestInterim = '';

    window.GameVoice._dotNetRef = dotNetRef;
    window.GameVoice._shouldListen = true;
    var rec = window.GameVoice._rec;
    if (!rec) {
      rec = new SR();
      rec.continuous = true;
      rec.interimResults = true;
      rec.lang = 'en-AU';

      rec.onresult = function (ev) {
        try {
          console.log('[GameVoice] onresult event received');
          if (!window.GameVoice._dotNetRef) {
            console.log('[GameVoice] no dotNetRef, ignoring');
            return;
          }
          var finalText = '';
          var latest = '';

          for (var i = ev.resultIndex; i < ev.results.length; i++) {
            var r = ev.results[i];
            if (!r || !r[0]) continue;
            var t = (r[0].transcript || '').trim();
            if (!t) continue;
            latest = t;
            if (r.isFinal) {
              if (!finalText || t.length > finalText.length) {
                finalText = t;
              }
            }
          }

          console.log('[GameVoice] finalText=', finalText, 'latest=', latest);
          // Treat final as a progressive transcript (Android often re-sends the whole phrase as final).
          if (finalText) {
            var cur = (window.GameVoice._bufferText || '').trim();
            var cand = finalText.trim();
            if (!cur) {
              window.GameVoice._bufferText = cand;
            } else if (cand === cur) {
              // no-op
            } else if (cand.startsWith(cur)) {
              window.GameVoice._bufferText = cand;
            } else if (cur.startsWith(cand)) {
              // keep cur
            } else {
              // Fallback: append if it's not a simple extension.
              window.GameVoice._bufferText = (cur + ' ' + cand).trim();
            }
            return;
          }

          // Track interim but do not send immediately (prevents repeats/truncation).
          if (latest && latest.length >= 2) {
            window.GameVoice._latestInterim = latest;
          }
        } catch (e) {
        }
      };

      rec.onerror = function (ev) {
        try {
          var msg = (ev && ev.error) ? ('' + ev.error) : 'error';
          var intentionalStop = !window.GameVoice._shouldListen;
          console.log('[GameVoice] onerror', msg, 'intentionalStop=', intentionalStop);

          if (intentionalStop && (msg === 'aborted' || msg === 'no-speech')) {
            return;
          }

          if (window.GameVoice._dotNetRef) {
            window.GameVoice._dotNetRef.invokeMethodAsync('OnVoiceError', msg);
          }
        } catch (e) {
        }
      };

      rec.onend = function () {
        try {
          window.GameVoice._listening = false;

          // Mobile browsers (esp Android) may end recognition frequently. Auto-restart
          // if Talk is still enabled (i.e., we didn't explicitly call stop()).
          if (window.GameVoice._shouldListen) {
            try {
              setTimeout(function () {
                try {
                  if (!window.GameVoice._shouldListen) return;
                  if (window.GameVoice._rec && !window.GameVoice._listening) {
                    window.GameVoice._rec.start();
                    window.GameVoice._listening = true;
                  }
                } catch (e2) {
                }
              }, 250);
            } catch (e1) {
            }
            return;
          }

          if (window.GameVoice._dotNetRef) {
            window.GameVoice._dotNetRef.invokeMethodAsync('OnVoiceEnded');
          }
        } catch (e) {
        }
      };

      window.GameVoice._rec = rec;
    }

    if (!window.GameVoice._listening) {
      console.log('[GameVoice.start] starting recognition');
      rec.start();
      window.GameVoice._listening = true;
    } else {
      console.log('[GameVoice.start] already listening');
    }
    return true;
  } catch (e) {
    return false;
  }
};

window.GameVoice.stop = function () {
  try {
    console.log('[GameVoice.stop] called, bufferText=', window.GameVoice._bufferText, 'latestInterim=', window.GameVoice._latestInterim);
    var rec = window.GameVoice._rec;
    window.GameVoice._shouldListen = false;

    var stopSession = window.GameVoice._sessionId || 0;

    try {
      if (window.GameVoice._flushTimer) {
        clearTimeout(window.GameVoice._flushTimer);
        window.GameVoice._flushTimer = null;
      }
    } catch (e1) {
    }

    try {
      if (window.GameVoice._stopFlushTimer) {
        clearTimeout(window.GameVoice._stopFlushTimer);
        window.GameVoice._stopFlushTimer = null;
      }
    } catch (eStopTimer1) {
    }

    // Stop first; engines often deliver the last final words right after stop().
    if (rec && window.GameVoice._listening) {
      try {
        rec.stop();
      } catch (eStop) {
      }
    }
    window.GameVoice._listening = false;

    // Flush once after a short delay.
    try {
      window.GameVoice._stopFlushTimer = setTimeout(function () {
        try {
          // If a new push-to-talk session started, do not flush stale text.
          if ((window.GameVoice._sessionId || 0) !== stopSession) return;
          window.GameVoice._flush(true);
        } catch (e0) {
        }
      }, 600);
    } catch (e2) {
    }

    return true;
  } catch (e) {
    window.GameVoice._shouldListen = false;
    try {
      if (window.GameVoice._flushTimer) {
        clearTimeout(window.GameVoice._flushTimer);
        window.GameVoice._flushTimer = null;
      }
    } catch (e2) {
    }
    window.GameVoice._listening = false;
    return false;
  }
 };

 window.GameVoice.ping = function () {
  try {
    return window.GameVoice.version || 'unknown';
  } catch (e) {
    return 'ping-error';
  }
 };

 window.GameVoice.getVoices = function () {
  try {
    var vs = (window.speechSynthesis && window.speechSynthesis.getVoices)
      ? window.speechSynthesis.getVoices()
      : [];

    return (vs || []).map(function (v) {
      return {
        VoiceURI: ((v && v.voiceURI) || '').trim(),
        Name: ((v && v.name) || '').trim(),
        Lang: ((v && v.lang) || '').trim()
      };
    });
  } catch (e) {
    return [];
  }
 };

 window.GameVoice.getVoicesCount = function () {
  try {
    var vs = window.GameVoice.getVoices();
    return (vs && vs.length) ? vs.length : 0;
  } catch (e) {
    return -1;
  }
 };

 window.GameVoice.getVoiceDiagnostics = function () {
  try {
    var hasSpeechSynthesis = !!(window.speechSynthesis && window.speechSynthesis.getVoices);
    var hasUtterance = !!window.SpeechSynthesisUtterance;
    var voiceCount = 0;

    try {
      voiceCount = window.GameVoice.getVoicesCount();
    } catch (e0) {
      voiceCount = -1;
    }

    return {
      hasSpeechSynthesis: hasSpeechSynthesis,
      hasUtterance: hasUtterance,
      voiceCount: voiceCount
    };
  } catch (e) {
    return {
      hasSpeechSynthesis: false,
      hasUtterance: false,
      voiceCount: -1
    };
  }
 };

 window.GameVoice.getVoicesAsync = function () {
  try {
    return new Promise(function (resolve) {
      try {
        var resolved = false;

        var finish = function (voices) {
          if (resolved) return;
          resolved = true;
          resolve(voices || []);
        };

        var tick = function () {
          try {
            try { window.speechSynthesis.getVoices(); } catch (eKick) { }

            var existing = window.GameVoice.getVoices();
            if (existing && existing.length) {
              finish(existing);
              return;
            }
          } catch (eTick) {
          }
        };

        tick();
        if (resolved) return;

        var pollCount = 0;
        var maxPollCount = 80;
        var poller = setInterval(function () {
          pollCount++;
          tick();
          if (resolved) {
            try { clearInterval(poller); } catch (eClear0) { }
            return;
          }
          if (pollCount >= maxPollCount) {
            try { clearInterval(poller); } catch (eClear1) { }
            finish([]);
          }
        }, 200);

        try {
          if (window.speechSynthesis) {
            window.speechSynthesis.onvoiceschanged = function () {
              try {
                tick();
                if (resolved) {
                  try { clearInterval(poller); } catch (eClear2) { }
                }
              } catch (eVoicesChanged) {
              }
            };
          }
        } catch (eWire) {
        }
      } catch (eOuter) {
        resolve([]);
      }
    });
  } catch (e) {
    return Promise.resolve([]);
  }
 };

 window.GameVoice.getVoicesAsyncCount = async function () {
  try {
    var vs = await window.GameVoice.getVoicesAsync();
    return (vs && vs.length) ? vs.length : 0;
  } catch (e) {
    return -1;
  }
 };

 window.GameVoice.refreshVoices = function () {
  try {
    if (window.speechSynthesis && window.speechSynthesis.getVoices) {
      window.speechSynthesis.getVoices();
    }
    return true;
  } catch (e) {
    return false;
  }
 };

 window.GameVoice.warmupVoices = function () {
  try {
    if (!window.speechSynthesis || !window.speechSynthesis.getVoices) return false;

    try {
      window.speechSynthesis.resume();
    } catch (eResume) {
    }

    try {
      window.speechSynthesis.getVoices();
    } catch (e0) {
    }

    try {
      if (typeof window.GameVoice.refreshVoices === 'function') {
        window.GameVoice.refreshVoices();
      }
    } catch (e1) {
    }

    try {
      if (window.SpeechSynthesisUtterance) {
        var probe = new SpeechSynthesisUtterance(' ');
        probe.volume = 0;
        probe.rate = 1;
        probe.pitch = 1;
        window.speechSynthesis.speak(probe);
      }
    } catch (e2) {
    }

    return true;
  } catch (e) {
    return false;
  }
 };

 window.GameVoice.getTalkEnabled = function () {
  try {
    if (!window.localStorage) return false;
    return window.localStorage.getItem('GameVoiceTalkEnabled') === '1';
  } catch (e) {
    return false;
  }
};

window.GameVoice.getBrowserFamily = function () {
  try {
    var ua = (navigator && navigator.userAgent) ? navigator.userAgent : '';
    ua = (ua || '').toLowerCase();

    if (ua.indexOf('edg/') >= 0) return 'edge';
    if (ua.indexOf('chrome/') >= 0 || ua.indexOf('chromium/') >= 0) return 'chrome';
    return 'other';
  } catch (e) {
    return 'other';
  }
};

window.GameVoice.getMicrosoftVoicesAsync = async function () {
  try {
    var voices = [];
    try {
      voices = await window.GameVoice.getVoicesAsync();
    } catch (e0) {
      voices = [];
    }

    return (voices || [])
      .map(function (v) {
        return {
          voiceURI: ((v && v.VoiceURI) || '').trim(),
          name: ((v && v.Name) || '').trim(),
          lang: ((v && v.Lang) || '').trim()
        };
      });
  } catch (e) {
    return [];
  }
};

window.GameVoice.setTalkEnabled = function (enabled) {
  try {
    if (!window.localStorage) return false;
    window.localStorage.setItem('GameVoiceTalkEnabled', enabled ? '1' : '0');
    return true;
  } catch (e) {
    return false;
  }
};

window.GameVoice._selectVoice = function (preferredKey, preferredLanguage, preferredRegion) {
  try {
    preferredKey = (preferredKey || '').trim();
    preferredLanguage = (preferredLanguage || '').trim().toLowerCase();
    preferredRegion = (preferredRegion || '').trim().toUpperCase();

    var normalizeText = function (value) {
      return ((value || '').trim().toLowerCase())
        .replace(/[_-]+/g, ' ')
        .replace(/\([^)]*\)/g, ' ')
        .replace(/[^a-z0-9 ]+/g, ' ')
        .replace(/\s+/g, ' ')
        .trim();
    };

    var getLanguagePart = function (lang) {
      lang = ((lang || '').trim()).toLowerCase();
      if (!lang) return '';
      var sep = lang.indexOf('-');
      if (sep < 0) sep = lang.indexOf('_');
      if (sep < 0) return lang;
      if (sep <= 0) return '';
      return lang.substring(0, sep).trim();
    };

    var getRegionPart = function (lang) {
      lang = ((lang || '').trim()).toLowerCase();
      if (!lang) return '';
      var sep = lang.indexOf('-');
      if (sep < 0) sep = lang.indexOf('_');
      if (sep < 0 || sep + 1 >= lang.length) return '';
      return lang.substring(sep + 1).trim().toUpperCase();
    };

    var findByNormalizedName = function (name, langHint) {
      var normalizedName = normalizeText(name);
      if (!normalizedName) return null;

      var langPart = getLanguagePart(langHint);
      var regionPart = getRegionPart(langHint);

      var candidates = vs.filter(function (v) {
        return normalizeText(v.name || '') === normalizedName;
      });
      if (!candidates.length) return null;

      if (langHint) {
        var exactLang = candidates.find(function (v) {
          return ((v.lang || '').trim()).toLowerCase() === langHint.toLowerCase();
        }) || null;
        if (exactLang) return exactLang;
      }

      if (langPart && regionPart) {
        var exactParts = candidates.find(function (v) {
          return getLanguagePart(v.lang) === langPart && getRegionPart(v.lang) === regionPart;
        }) || null;
        if (exactParts) return exactParts;
      }

      if (langPart) {
        var sameLanguage = candidates.find(function (v) {
          return getLanguagePart(v.lang) === langPart;
        }) || null;
        if (sameLanguage) return sameLanguage;
      }

      return candidates[0] || null;
    };

    var vs = (window.speechSynthesis && window.speechSynthesis.getVoices)
      ? window.speechSynthesis.getVoices()
      : [];
    if (!vs || !vs.length) return null;

    var match = null;

    if (preferredKey.indexOf('uri:') === 0) {
      var uri = preferredKey.substring('uri:'.length);
      match = vs.find(function (v) { return (v.voiceURI || '') === uri; }) || null;

      if (!match) {
        match = findByNormalizedName(uri, preferredLanguage && preferredRegion ? (preferredLanguage + '-' + preferredRegion) : preferredLanguage);
      }
    }

    if (!match && preferredKey.indexOf('name:') === 0) {
      var parts = preferredKey.split('|lang:');
      var name = (parts[0] || '').substring('name:'.length);
      var lang = (parts[1] || '').trim();

      match = vs.find(function (v) {
        var vn = (v.name || '');
        var vl = (v.lang || '');
        if (!name) return false;
        if (lang) return (vn === name) && (vl === lang);
        return vn === name;
      }) || null;

      if (!match) {
        match = findByNormalizedName(name, lang);
      }
    }

    if (!match && preferredKey.indexOf('name:') === 0) {
      var nameOnly = (preferredKey.split('|')[0] || '').substring('name:'.length);
      match = vs.find(function (v) { return (v.name || '') === nameOnly; }) || null;

      if (!match) {
        match = findByNormalizedName(nameOnly, preferredLanguage && preferredRegion ? (preferredLanguage + '-' + preferredRegion) : preferredLanguage);
      }
    }

    if (!match && preferredLanguage) {
      var exactLang = preferredRegion ? (preferredLanguage + '-' + preferredRegion).toLowerCase() : '';

      if (exactLang) {
        match = vs.find(function (v) {
          var vl = ((v.lang || '').trim()).toLowerCase();
          return vl === exactLang;
        }) || null;
      }

      if (!match) {
        match = vs.find(function (v) {
          var vl = ((v.lang || '').trim()).toLowerCase();
          return vl === preferredLanguage || vl.indexOf(preferredLanguage + '-') === 0 || vl.indexOf(preferredLanguage + '_') === 0;
        }) || null;
      }
    }

    return match;
  } catch (e) {
    return null;
  }
};

window.GameVoice.describeVoice = function (preferredKey, preferredLanguage, preferredRegion) {
  try {
    preferredKey = (preferredKey || '').trim();
    var v = null;
    try { v = window.GameVoice._selectVoice(preferredKey, preferredLanguage, preferredRegion); } catch (eSel) { v = null; }

    if (!v) {
      if (!preferredKey) return 'Default voice';
      return 'Default voice (preferredKey=' + preferredKey + ')';
    }

    var name = (v.name || '').trim();
    var lang = (v.lang || '').trim();
    var uri = (v.voiceURI || '').trim();

    var label = name;
    if (lang) label = label ? (label + ' (' + lang + ')') : ('(' + lang + ')');
    if (uri) label = label ? (label + ' uri=' + uri) : ('uri=' + uri);
    if (!label) label = 'Selected voice';
    if (label.length > 160) label = label.substring(0, 160);
    return label;
  } catch (e) {
    return 'Default voice';
  }
};

window.GameVoice.inspectVoiceMatch = async function (preferredKey, preferredLanguage, preferredRegion) {
  try {
    preferredKey = (preferredKey || '').trim();
    preferredLanguage = (preferredLanguage || '').trim();
    preferredRegion = (preferredRegion || '').trim();

    try {
      await window.GameVoice.getVoicesAsync();
    } catch (eLoad) {
    }

    var voices = [];
    try {
      voices = (window.speechSynthesis && window.speechSynthesis.getVoices)
        ? (window.speechSynthesis.getVoices() || [])
        : [];
    } catch (e0) {
      voices = [];
    }

    var matched = null;
    try {
      matched = window.GameVoice._selectVoice(preferredKey, preferredLanguage, preferredRegion);
    } catch (eSel) {
      matched = null;
    }

    var matchedLabel = '';
    try {
      if (matched) {
        var name = (matched.name || '').trim();
        var lang = (matched.lang || '').trim();
        var uri = (matched.voiceURI || '').trim();
        matchedLabel = name;
        if (lang) matchedLabel = matchedLabel ? (matchedLabel + ' (' + lang + ')') : ('(' + lang + ')');
        if (uri) matchedLabel = matchedLabel ? (matchedLabel + ' uri=' + uri) : ('uri=' + uri);
      }
    } catch (e1) {
      matchedLabel = '';
    }

    var sample = [];
    try {
      sample = (voices || []).slice(0, 8).map(function (v) {
        var name = ((v && v.name) || '').trim();
        var lang = ((v && v.lang) || '').trim();
        var uri = ((v && v.voiceURI) || '').trim();
        var label = name;
        if (lang) label = label ? (label + ' (' + lang + ')') : ('(' + lang + ')');
        if (uri) label = label ? (label + ' uri=' + uri) : ('uri=' + uri);
        return label;
      });
    } catch (e2) {
      sample = [];
    }

    return {
      preferredKey: preferredKey,
      preferredLanguage: preferredLanguage,
      preferredRegion: preferredRegion,
      voicesCount: (voices || []).length || 0,
      matched: !!matched,
      matchedLabel: matchedLabel,
      sampleVoices: sample
    };
  } catch (e) {
    return {
      preferredKey: (preferredKey || '').trim(),
      preferredLanguage: (preferredLanguage || '').trim(),
      preferredRegion: (preferredRegion || '').trim(),
      voicesCount: -1,
      matched: false,
      matchedLabel: '',
      sampleVoices: []
    };
  }
};

window.GameVoice.speak = async function (text, preferredKey, preferredLanguage, preferredRegion) {
  try {
    console.log('[GameVoice.speak] text=', text, 'preferredKey=', preferredKey, 'preferredLanguage=', preferredLanguage, 'preferredRegion=', preferredRegion);
    if (!text) return false;
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) return false;
    window.GameVoice.unlockTts();

    try {
      await window.GameVoice.getVoicesAsync();
    } catch (eLoad) {
    }

    try {
      window.speechSynthesis.resume();
    } catch (eResume0) {
    }

    try {
      if (window.speechSynthesis.speaking || window.speechSynthesis.pending) {
        window.speechSynthesis.cancel();
      }
    } catch (eCancel0) {
    }

    var u = new SpeechSynthesisUtterance(text);
    u.rate = 1;
    u.pitch = 1;
    u.volume = 1;

    try {
      var lang = '';
      preferredLanguage = (preferredLanguage || '').trim().toLowerCase();
      preferredRegion = (preferredRegion || '').trim().toUpperCase();
      if (preferredLanguage && preferredRegion) {
        lang = preferredLanguage + '-' + preferredRegion;
      } else if (preferredLanguage) {
        lang = preferredLanguage;
      }
      if (lang) {
        u.lang = lang;
      }
    } catch (eLang0) {
    }

    try {
      var v = window.GameVoice._selectVoice(preferredKey, preferredLanguage, preferredRegion);
      if (v) {
        u.voice = v;
        if (!u.lang && v.lang) {
          u.lang = v.lang;
        }
      }
    } catch (ePick) {
    }

    window.speechSynthesis.speak(u);
    return true;
  } catch (e) {
    return false;
  }
};

window.GameVoice.speakDefault = async function (text) {
  try {
    console.log('[GameVoice.speakDefault] text=', text);
    if (!text) return false;
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) return false;
    window.GameVoice.unlockTts();

    try {
      window.speechSynthesis.resume();
    } catch (eResume0) {
    }

    try {
      if (window.speechSynthesis.speaking || window.speechSynthesis.pending) {
        window.speechSynthesis.cancel();
      }
    } catch (eCancel0) {
    }

    var u = new SpeechSynthesisUtterance(text);
    u.rate = 1;
    u.pitch = 1;
    u.volume = 1;

    window.speechSynthesis.speak(u);
    return true;
  } catch (e) {
    return false;
  }
};

window.GameVoice.speakAndDescribe = async function (text, preferredKey, preferredLanguage, preferredRegion) {
  try {
    console.log('[GameVoice.speakAndDescribe] text=', text, 'preferredKey=', preferredKey, 'preferredLanguage=', preferredLanguage, 'preferredRegion=', preferredRegion);
    if (!text) return '';
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) return '';
    window.GameVoice.unlockTts();

    try {
      await window.GameVoice.getVoicesAsync();
    } catch (eLoad) {
    }

    try {
      window.speechSynthesis.resume();
    } catch (eResume0) {
    }

    try {
      if (window.speechSynthesis.speaking || window.speechSynthesis.pending) {
        window.speechSynthesis.cancel();
      }
    } catch (eCancel0) {
    }

    var u = new SpeechSynthesisUtterance(text);
    u.rate = 1;
    u.pitch = 1;
    u.volume = 1;

    try {
      var lang = '';
      preferredLanguage = (preferredLanguage || '').trim().toLowerCase();
      preferredRegion = (preferredRegion || '').trim().toUpperCase();
      if (preferredLanguage && preferredRegion) {
        lang = preferredLanguage + '-' + preferredRegion;
      } else if (preferredLanguage) {
        lang = preferredLanguage;
      }
      if (lang) {
        u.lang = lang;
      }
    } catch (eLang0) {
    }

    var applied = null;
    try {
      applied = window.GameVoice._selectVoice(preferredKey, preferredLanguage, preferredRegion);
      if (applied) {
        u.voice = applied;
        if (!u.lang && applied.lang) {
          u.lang = applied.lang;
        }
      }
    } catch (ePick) {
      applied = null;
    }

    if (!applied) {
      try {
        await window.GameVoice.getVoicesAsync();
        applied = window.GameVoice._selectVoice(preferredKey, preferredLanguage, preferredRegion);
        if (applied) {
          u.voice = applied;
          if (!u.lang && applied.lang) {
            u.lang = applied.lang;
          }
        }
      } catch (ePick2) {
        applied = applied || null;
      }
    }

    window.speechSynthesis.speak(u);

    try {
      var finalApplied = null;
      try {
        finalApplied = u.voice || applied || window.GameVoice._selectVoice(preferredKey, preferredLanguage, preferredRegion);
      } catch (eFinal) {
        finalApplied = u.voice || applied || null;
      }

      if (!finalApplied) {
        return 'Default voice';
      }

      var name = (finalApplied.name || '').trim();
      var langUsed = (finalApplied.lang || '').trim();
      var uri = (finalApplied.voiceURI || '').trim();

      var label = name;
      if (langUsed) label = label ? (label + ' (' + langUsed + ')') : ('(' + langUsed + ')');
      if (uri) label = label ? (label + ' uri=' + uri) : ('uri=' + uri);
      if (!label) label = 'Selected voice';
      if (label.length > 160) label = label.substring(0, 160);
      return label;
    } catch (eLabel) {
      return (u.voice || applied) ? 'Selected voice' : 'Default voice';
    }
  } catch (e) {
    try {
      var fallback = window.GameVoice.describeVoice(preferredKey, preferredLanguage, preferredRegion);
      if (fallback && fallback.trim()) {
        return fallback;
      }
    } catch (eDescribe) {
    }

    try {
      var msg = (e && e.message) ? ('' + e.message) : 'speak failed';
      if (msg.length > 120) msg = msg.substring(0, 120);
      return 'Speak failed: ' + msg;
    } catch (eMsg) {
      return 'Speak failed';
    }
  }
};

window.GameVoice.speakDefaultAndDescribe = async function (text) {
  try {
    console.log('[GameVoice.speakDefaultAndDescribe] text=', text);
    if (!text) return '';
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) return '';
    window.GameVoice.unlockTts();

    try {
      window.speechSynthesis.resume();
    } catch (eResume0) {
    }

    try {
      if (window.speechSynthesis.speaking || window.speechSynthesis.pending) {
        window.speechSynthesis.cancel();
      }
    } catch (eCancel0) {
    }

    var u = new SpeechSynthesisUtterance(text);
    u.rate = 1;
    u.pitch = 1;
    u.volume = 1;

    window.speechSynthesis.speak(u);
    return 'Default voice';
  } catch (e) {
    try {
      var msg = (e && e.message) ? ('' + e.message) : 'speak failed';
      if (msg.length > 120) msg = msg.substring(0, 120);
      return 'Speak failed: ' + msg;
    } catch (eMsg) {
      return 'Speak failed';
    }
  }
};

window.GameVoice.getTtsState = function () {
  try {
    var has = !!(window.speechSynthesis && window.SpeechSynthesisUtterance);
    var paused = false;
    var speaking = false;
    var pending = false;
    var voicesCount = 0;

    if (has) {
      try { paused = !!window.speechSynthesis.paused; } catch (e0) { }
      try { speaking = !!window.speechSynthesis.speaking; } catch (e1) { }
      try { pending = !!window.speechSynthesis.pending; } catch (e2) { }
      try {
        var v = window.speechSynthesis.getVoices();
        voicesCount = v ? v.length : 0;
      } catch (e3) { }
    }

    return {
      has: has,
      paused: paused,
      speaking: speaking,
      pending: pending,
      voicesCount: voicesCount,
      nowMs: Date.now()
    };
  } catch (e) {
    return { has: false, paused: false, speaking: false, pending: false, voicesCount: 0, nowMs: Date.now() };
  }
};

window.GameVoice.cancel = function () {
  try {
    if (!window.speechSynthesis) return false;
    window.speechSynthesis.cancel();
    return true;
  } catch (e) {
    return false;
  }
};
