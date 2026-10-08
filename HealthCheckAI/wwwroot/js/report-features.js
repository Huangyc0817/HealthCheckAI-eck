<script>
    if (!window.__ttsBound) {
        window.__ttsBound = true;

    window.reportAudioPlayer = new Audio();
    let isFetchingAudio = false;

    document.addEventListener("click", async function (e) {
            const btn = e.target.closest("button");
    if (!btn) return;

    const id = btn.id;

    if (id === "playButton") {
                if (isFetchingAudio) return;

    window.reportAudioPlayer.pause();
    window.reportAudioPlayer.currentTime = 0;

    // 改成這樣
    let textToSpeak = window.currentReportText || "";

    if (localStorage.getItem("siteLang") === "en") {
        textToSpeak = Array.from(document.querySelectorAll(".translate-target"))
            .map(el => el.innerText.trim())
            .filter(x => x)
            .join("\n\n");
                }

    if (!textToSpeak) return;

    const originalText = btn.innerText;
    btn.innerText = "⏳ 載入語音中...";
    isFetchingAudio = true;

    try {
                    const timestamp = new Date().getTime();
    const response = await fetch(`/TTS/Speak?text=${encodeURIComponent(textToSpeak)}&t=${timestamp}`);

    if (!response.ok) throw new Error("語音 API 回應錯誤");

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);

    window.reportAudioPlayer.src = url;
    window.reportAudioPlayer.play();

    const pauseBtn = document.getElementById("pauseButton");
    if (pauseBtn) pauseBtn.innerText = "⏸ 暫停";
                } catch (err) {
        console.error("語音播放失敗：", err);
    alert("AI 語音載入失敗，請稍後再試。");
                } finally {
        btn.innerText = originalText;
    isFetchingAudio = false;
                }
            }

    if (id === "pauseButton") {
                if (!window.reportAudioPlayer.src) return;

    if (window.reportAudioPlayer.paused) {
        window.reportAudioPlayer.play();
    btn.innerText = "⏸ 暫停";
                } else {
        window.reportAudioPlayer.pause();
    btn.innerText = "▶ 繼續";
                }
            }

    if (id === "stopButton") {
                if (window.reportAudioPlayer.src) {
        window.reportAudioPlayer.pause();
    window.reportAudioPlayer.currentTime = 0;
                }
    const pauseBtn = document.getElementById("pauseButton");
    if (pauseBtn) pauseBtn.innerText = "⏸ 暫停";
            }
        });

    window.reportAudioPlayer.addEventListener('ended', function() {
             const pauseBtn = document.getElementById("pauseButton");
    if (pauseBtn) pauseBtn.innerText = "⏸ 暫停";
        });
    }

    // 翻譯功能
    let originalTexts = new Map();

    async function translateText(text, targetLang) {
        const response = await fetch("/api/translate", {
        method: "POST",
    headers: {
        "Content-Type": "application/json"
            },
    body: JSON.stringify({
        text: text,
    source: "auto",
    target: targetLang
            })
        });

    if (!response.ok) {
            throw new Error("翻譯失敗");
        }

    const data = await response.json();
    return data.translatedText;
    }

    async function translatePageToEnglish() {
        const elements = document.querySelectorAll("[data-translate], .translate-target");

    for (const el of elements) {
            const original = originalTexts.get(el) || el.innerText.trim();

    if (!originalTexts.has(el)) {
        originalTexts.set(el, original);
            }

    if (!original) continue;

    try {
                const translated = await translateText(original, "en");
    el.innerText = translated;
            } catch (err) {
        console.error("翻譯失敗：", err);
            }
        }
    }

    document.addEventListener("DOMContentLoaded", function () {
        const savedLang = localStorage.getItem("siteLang") || "zh";

    if (savedLang === "en") {
        translatePageToEnglish();
        }
    });
</script>