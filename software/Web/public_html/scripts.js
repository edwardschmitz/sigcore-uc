// ========================
// Header & Footer Loading
// ========================
document.addEventListener("DOMContentLoaded", () => {
    loadExternal("header.html", "header-container");
    loadExternal("footer.html", "footer-container");
    loadImages(); 
    loadMediaList(); 
});

function loadExternal(file, containerId) {
    fetch(file)
        .then(response => response.text())
        .then(data => {
            const container = document.getElementById(containerId);
            if (container) container.innerHTML = data;
        })
        .catch(error => console.error(`Error loading ${file}:`, error));
}

// ========================
// Slideshow Logic
// ========================
let slideIndex = 0;
let slides = [];
let slideshowInterval;
let isPlaying = true;
let imageSources = [];

function loadImages() {
    fetch('/get-images.php') // Adjust path if needed
        .then(response => response.json())
        .then(images => {
            const slideshow = document.getElementById("slideshow");
            if (!slideshow) return;
            slideshow.innerHTML = "";
            imageSources = images;

            images.forEach((src, index) => {
                const slide = document.createElement("div");
                slide.classList.add("slide");
                if (index === 0) slide.classList.add("active");

                const img = document.createElement("img");
                img.src = src;
                img.alt = `Slideshow Image ${index + 1}`;
                img.addEventListener("click", () => openModal(src));

                slide.appendChild(img);
                slideshow.appendChild(slide);
            });

            slides = document.querySelectorAll(".slide");
            startSlideshow();
        })
        .catch(error => console.error("Error loading images:", error));
}

function showSlides() {
    slides.forEach((slide, i) => {
        slide.style.opacity = "0";
        slide.style.zIndex = "1";
    });

    slideIndex = (slideIndex + 1) % slides.length;
    slides[slideIndex].style.opacity = "1";
    slides[slideIndex].style.zIndex = "10";
}

function changeSlide(n) {
    stopSlideshow();
    slides.forEach(slide => slide.classList.remove("active"));
    slideIndex = (slideIndex + n + slides.length) % slides.length;
    slides[slideIndex].classList.add("active");
}

function startSlideshow() {
    slideshowInterval = setInterval(showSlides, 4000);
}

function stopSlideshow() {
    clearInterval(slideshowInterval);
}

function toggleSlideshow() {
    const button = document.querySelector(".pause-play");
    if (isPlaying) {
        stopSlideshow();
        button.textContent = "Play";
    } else {
        startSlideshow();
        button.textContent = "Pause";
    }
    isPlaying = !isPlaying;
}

// ========================
// Modal Logic
// ========================
function openModal(imageSrc) {
    const modal = document.getElementById("imageModal");
    const modalImg = document.getElementById("modalImage");
    modal.style.display = "flex";
    modalImg.src = imageSrc;
}

function closeModal() {
    document.getElementById("imageModal").style.display = "none";
}

// ========================
// Mobile Menu Toggle
// ========================
function toggleMobileMenu() {
    const menu = document.querySelector(".nav-menu");
    if (menu) menu.classList.toggle("show");
}

// ========================
// Load Media Previews
// ========================
function loadMediaList() {
    fetch('media.json')
        .then(res => res.json())
        .then(data => {
            const container = document.getElementById("media-list");

            data.forEach(entry => {
                const linkWrapper = document.createElement("a");
                linkWrapper.href = entry.url;
                linkWrapper.target = "_blank";
                linkWrapper.className = "card-link-wrapper";

                const card = document.createElement("div");
                card.className = "card";

                const cardContent = document.createElement("div");
                cardContent.className = "card-content";

                // Site name (extracted from URL)
                const site = document.createElement("div");
                site.className = "note";
                site.textContent = extractHostname(entry.url);
                cardContent.appendChild(site);

                if (entry.image) {
                    const imageContainer = document.createElement("div");
                    imageContainer.className = "image-container";

                    const img = document.createElement("img");
                    img.src = entry.image;
                    img.alt = entry.label;
                    img.style.maxHeight = "140px";
                    img.style.objectFit = "contain";
                    img.style.marginTop = "10px";

                    imageContainer.appendChild(img);
                    cardContent.appendChild(imageContainer);
                }

                // Title
                const title = document.createElement("h2");
                title.textContent = entry.label;
                cardContent.appendChild(title);

                // Description
                if (entry.description) {
                    const desc = document.createElement("p");
                    desc.textContent = entry.description;
                    cardContent.appendChild(desc);
                }

                //// Button (must be inside .card-content to stay pinned)
                //const button = document.createElement("div");
                //button.className = "view-article-button";
                //button.textContent = "View Article";
                //cardContent.appendChild(button);

                // Build full card
                card.appendChild(cardContent);
                linkWrapper.appendChild(card);
                container.appendChild(linkWrapper);
            });
        })
        .catch(err => {
            const fallback = document.createElement("p");
            fallback.textContent = "Failed to load media list.";
            document.getElementById("media-list").appendChild(fallback);
            console.error(err);
        });
}
function extractHostname(url) {
    try {
        return new URL(url).hostname.replace('www.', '');
    } catch {
        return '';
    }
}
