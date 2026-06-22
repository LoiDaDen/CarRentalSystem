document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("[data-tutorial-root]");
    if (!root || !window.bootstrap) return;

    const panel = root.dataset.panel;
    const modalElement = root.querySelector(".tutorial-modal");
    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    const focusRing = root.querySelector("[data-tutorial-focus-ring]");
    const title = root.querySelector("[data-tutorial-title]");
    const role = root.querySelector("[data-tutorial-role]");
    const stepTitle = root.querySelector("[data-tutorial-step-title]");
    const stepDescription = root.querySelector("[data-tutorial-step-description]");
    const icon = root.querySelector("[data-tutorial-icon]");
    const count = root.querySelector("[data-tutorial-step-count]");
    const progressBar = root.querySelector("[data-tutorial-progress-bar]");
    const routeLink = root.querySelector("[data-tutorial-route]");
    const backButton = root.querySelector("[data-tutorial-back]");
    const nextButton = root.querySelector("[data-tutorial-next]");
    const skipButton = root.querySelector("[data-tutorial-skip]");
    const neverButton = root.querySelector("[data-tutorial-never]");
    const antiforgery = root.querySelector("input[name='__RequestVerificationToken']")?.value || "";

    let tutorial = null;
    let activeIndex = 0;

    const sessionKey = (key) => `tutorial-skipped:${panel}:${key}`;

    const clearFocus = () => focusRing?.classList.remove("is-visible");
    const hideModal = () => {
        modal.hide();
        if (modalElement.classList.contains("show")) {
            modalElement.addEventListener("shown.bs.modal", () => modal.hide(), { once: true });
        }
    };

    const renderFocus = (target) => {
        clearFocus();
        if (!target || !focusRing) return;
        const element = document.querySelector(`[data-tutorial-target="${CSS.escape(target)}"]`);
        if (!element || element.offsetParent === null) return;
        const rect = element.getBoundingClientRect();
        const padding = 6;
        focusRing.style.left = `${Math.max(4, rect.left - padding)}px`;
        focusRing.style.top = `${Math.max(4, rect.top - padding)}px`;
        focusRing.style.width = `${Math.min(window.innerWidth - 8, rect.width + padding * 2)}px`;
        focusRing.style.height = `${rect.height + padding * 2}px`;
        focusRing.classList.add("is-visible");
    };

    const render = () => {
        const step = tutorial?.steps?.[activeIndex];
        if (!step) return;
        const total = tutorial.steps.length;
        title.textContent = tutorial.title;
        role.textContent = tutorial.roleName;
        stepTitle.textContent = step.title;
        stepDescription.textContent = step.description;
        icon.className = `bi ${step.icon || tutorial.icon || "bi-compass"}`;
        count.textContent = `${activeIndex + 1} / ${total}`;
        progressBar.style.width = `${((activeIndex + 1) / total) * 100}%`;
        backButton.disabled = activeIndex === 0;
        nextButton.innerHTML = activeIndex === total - 1
            ? 'Hoàn tất <i class="bi bi-check2"></i>'
            : 'Tiếp theo <i class="bi bi-arrow-right"></i>';
        if (step.route) {
            routeLink.href = step.route;
            routeLink.classList.remove("d-none");
        } else {
            routeLink.removeAttribute("href");
            routeLink.classList.add("d-none");
        }
        renderFocus(step.target);
    };

    const openTutorial = async (manual = false) => {
        const url = new URL(root.dataset.getUrl, window.location.origin);
        url.searchParams.set("panel", panel);
        url.searchParams.set("manual", manual ? "true" : "false");
        const response = await fetch(url, { headers: { Accept: "application/json" }, cache: "no-store" });
        if (response.status === 204 || !response.ok) return;
        const data = await response.json();
        if (!manual && sessionStorage.getItem(sessionKey(data.tutorialKey)) === "1") return;
        tutorial = data;
        activeIndex = 0;
        render();
        modal.show();
    };

    const updateProgress = async (action) => {
        if (!tutorial) return false;
        const response = await fetch(root.dataset.updateUrl, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": antiforgery
            },
            body: JSON.stringify({
                panel,
                tutorialKey: tutorial.tutorialKey,
                action
            })
        });
        return response.ok;
    };

    backButton.addEventListener("click", () => {
        activeIndex = Math.max(0, activeIndex - 1);
        render();
    });

    nextButton.addEventListener("click", async () => {
        if (!tutorial) return;
        if (activeIndex < tutorial.steps.length - 1) {
            activeIndex += 1;
            render();
            return;
        }
        if (await updateProgress("Finish")) hideModal();
    });

    skipButton.addEventListener("click", () => {
        if (tutorial) sessionStorage.setItem(sessionKey(tutorial.tutorialKey), "1");
        hideModal();
    });

    neverButton.addEventListener("click", async () => {
        if (await updateProgress("DoNotShowAgain")) hideModal();
    });

    document.querySelectorAll(`[data-tutorial-replay][data-panel="${CSS.escape(panel)}"]`).forEach((button) => {
        button.addEventListener("click", () => openTutorial(true));
    });

    modalElement.addEventListener("hidden.bs.modal", clearFocus);
    window.addEventListener("resize", () => {
        if (modalElement.classList.contains("show")) render();
    });
    document.addEventListener("keydown", (event) => {
        if (!modalElement.classList.contains("show")) return;
        if (event.key === "ArrowLeft") backButton.click();
        if (event.key === "ArrowRight") nextButton.click();
    });

    openTutorial(false).catch(() => {});
});
