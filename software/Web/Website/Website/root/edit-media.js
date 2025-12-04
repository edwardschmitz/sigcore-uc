// LinkPreview.io API Key
const LINK_PREVIEW_API_KEY = 'd8fe7f8acd93eca2f01f17d35bf2d884'; // Replace with your actual key

// ================
// DOM Ready
// ================
document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("fetch-preview").addEventListener("click", fetchPreview);
    loadMediaList();
});

// ================
// Fetch Preview
// ================
function fetchPreview() {
    const url = document.getElementById("media-url").value.trim();
    if (!url) return alert("Please enter a URL.");

    fetch(`https://api.linkpreview.net/?key=${LINK_PREVIEW_API_KEY}&q=${encodeURIComponent(url)}`)
        .then(res => res.json())
        .then(data => {
            if (!data.title || !data.url) throw new Error("Invalid preview data");
            renderPreview(data);
        })
        .catch(err => {
            alert("Failed to fetch preview.");
            console.error(err);
        });
}

// ================
// Render Preview
// ================
function renderPreview(data) {
    const container = document.getElementById("preview-container");
    container.innerHTML = '';

    const card = document.createElement("div");
    card.className = "card";

    const content = document.createElement("div");
    content.className = "card-content";

    const title = document.createElement("h2");
    title.textContent = data.title;

    const image = document.createElement("img");
    image.src = data.image;
    image.alt = data.title;
    image.style.maxWidth = "100%";

    const desc = document.createElement("p");
    desc.textContent = data.description || '';

    const link = document.createElement("a");
    link.href = data.url;
    link.textContent = "View Article";
    link.target = "_blank";

    const saveBtn = document.createElement("button");
    saveBtn.textContent = "Save";
    saveBtn.onclick = () => saveMedia(data);

    content.appendChild(title);
    content.appendChild(image);
    content.appendChild(desc);
    content.appendChild(link);
    content.appendChild(saveBtn);
    card.appendChild(content);
    container.appendChild(card);
}

// ================
// Save Media Entry
// ================
function saveMedia(data) {
    const newEntry = {
        label: data.title,
        url: data.url,
        image: data.image,
        description: data.description,
        date: new Date().toISOString()
    };

    fetch('media.json')
        .then(res => res.json())
        .then(existing => {
            existing.push(newEntry);
            existing.sort((a, b) => new Date(b.date) - new Date(a.date));
            return fetch('save-media.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(existing)
            });
        })
        .then(() => {
            alert("Saved!");
            document.getElementById("preview-container").innerHTML = '';
            loadMediaList();
        })
        .catch(err => {
            alert("Save failed.");
            console.error(err);
        });
}

// ================
// Load Existing List
// ================
function loadMediaList() {
    const list = document.getElementById("media-list");
    list.innerHTML = '';

    fetch('media.json')
        .then(res => res.json())
        .then(data => {
            data.sort((a, b) => new Date(b.date) - new Date(a.date));
            data.forEach(entry => {
                const card = document.createElement("div");
                card.className = "card";

                const content = document.createElement("div");
                content.className = "card-content";

                const title = document.createElement("h2");
                title.textContent = entry.label;

                const img = document.createElement("img");
                img.src = entry.image;
                img.alt = entry.label;

                const desc = document.createElement("p");
                desc.textContent = entry.description;

                const link = document.createElement("a");
                link.href = entry.url;
                link.target = "_blank";
                link.textContent = "View Article";

                const delBtn = document.createElement("button");
                delBtn.textContent = "Delete";
                delBtn.onclick = () => deleteEntry(entry.url);

                content.appendChild(title);
                content.appendChild(img);
                content.appendChild(desc);
                content.appendChild(link);
                content.appendChild(delBtn);

                card.appendChild(content);
                list.appendChild(card);
            });
        })
        .catch(err => {
            list.textContent = "Failed to load media list.";
            console.error(err);
        });
}

// ================
// Delete Entry
// ================
function deleteEntry(urlToDelete) {
    if (!confirm("Are you sure you want to delete this entry?")) return;

    fetch('media.json')
        .then(res => res.json())
        .then(existing => {
            const updated = existing.filter(entry => entry.url !== urlToDelete);
            return fetch('save-media.php', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(updated)
            });
        })
        .then(() => loadMediaList())
        .catch(err => {
            alert("Delete failed.");
            console.error(err);
        });
}
