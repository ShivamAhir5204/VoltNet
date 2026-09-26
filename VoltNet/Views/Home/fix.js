const fs = require('fs');

try {
    let content = fs.readFileSync('Index.cshtml', 'utf8');

    // 1. Replace HTML img tags
    const htmlRegex = /<img\s+id="heroChargerFrame"[\s\S]*?\/>/;
    const newHtml = `<img
                id="heroChargerFrame1"
                src="~/usersite/img/hero-frames/frame_001.jpg"
                alt="VoltNet EV Charging Station"
                class="charger-frame-img img-fluid position-absolute top-0 start-0 w-100 h-100"
                style="object-fit: contain; transition: opacity 0.2s ease-in-out; opacity: 1; z-index: 2;" onerror="this.onerror=null; this.src='/usersite/img/hero-charger12.png';"
            />
            <img
                id="heroChargerFrame2"
                src="~/usersite/img/hero-frames/frame_001.jpg"
                alt="VoltNet EV Charging Station"
                class="charger-frame-img img-fluid position-absolute top-0 start-0 w-100 h-100"
                style="object-fit: contain; transition: opacity 0.2s ease-in-out; opacity: 0; z-index: 1;"
            />`;
    
    content = content.replace(htmlRegex, newHtml);

    // 2. Replace JS vars
    const jsVarsRegex = /const heroImgElement = document.getElementById\('heroChargerFrame'\);\s*if \(!heroImgElement\) return;/;
    const newJsVars = `const img1 = document.getElementById('heroChargerFrame1');
    const img2 = document.getElementById('heroChargerFrame2');
    let activeImg = 1;
    
    if (!img1 || !img2) return;`;
    
    content = content.replace(jsVarsRegex, newJsVars);

    fs.writeFileSync('Index.cshtml', content, 'utf8');
    console.log('Success');
} catch (e) {
    console.error(e);
}
