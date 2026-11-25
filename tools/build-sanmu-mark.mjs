// Bake three embossed 🌲 glyphs into one transparent, high-DPI watermark.
// Authoring only: npm install --prefix .release/clock-art three@0.180.0 @xmldom/xmldom@0.9.12 @resvg/resvg-js@2.6.2
import {readFileSync, writeFileSync} from 'node:fs';
import {createRequire} from 'node:module';
import {pathToFileURL} from 'node:url';

const require = createRequire(new URL('../.release/clock-art/package.json', import.meta.url));
const THREE = await import(new URL('three.module.js', pathToFileURL(require.resolve('three'))));
const {SVGLoader} = await import(pathToFileURL(require.resolve('three/addons/loaders/SVGLoader.js')));
const {SVGRenderer} = await import(pathToFileURL(require.resolve('three/addons/renderers/SVGRenderer.js')));
const {DOMParser, DOMImplementation, XMLSerializer} = require('@xmldom/xmldom');
const {Resvg} = require('@resvg/resvg-js');
globalThis.DOMParser = DOMParser;
globalThis.document = new DOMImplementation().createDocument('http://www.w3.org/2000/svg', 'svg', null);

const source = readFileSync(new URL('art/evergreen.svg', import.meta.url), 'utf8');
const {paths} = new SVGLoader().parse(source);
const tree = new THREE.Group();
const enamel = {'#662113': 0x73786c, '#5c913b': 0x8e9b87, '#3e721d': 0x687963};
paths.forEach((path, layer) => {
    const surface = new THREE.MeshLambertMaterial({color: enamel[path.userData.style.fill.toLowerCase()]});
    const shape = new THREE.ExtrudeGeometry(SVGLoader.createShapes(path), {
        depth: 1.0, bevelEnabled: true, bevelThickness: .32, bevelSize: .30, bevelSegments: 2, curveSegments: 8
    });
    shape.translate(-18, -18, 0).rotateX(Math.PI).translate(0, 0, layer * .55);
    const relief = new THREE.Mesh(shape, surface);
    relief.renderOrder = layer; // Preserve the emoji's overlapping boughs in the vector renderer.
    tree.add(relief);
});
tree.rotation.set(-.12, -.20, 0);

const scene = new THREE.Scene();
for (const [x, y] of [[0, 18], [-17.5, -17], [17.5, -17]]) {
    const copy = tree.clone();
    copy.position.set(x, y, 0);
    scene.add(copy);
}
scene.add(new THREE.AmbientLight(0xffffff, .68));
const key = new THREE.DirectionalLight(0xfffaf0, .7);
key.position.set(-3, 5, 6);
scene.add(key);
const camera = new THREE.OrthographicCamera(-37, 37, 38, -36, .1, 150);
camera.position.z = 80;
const renderer = new SVGRenderer();
renderer.autoClear = false;
renderer.setSize(192, 192);
renderer.setPrecision(2);
renderer.render(scene, camera);
const svg = new XMLSerializer().serializeToString(renderer.domElement);
const ink = new Resvg(svg).render().asPng();
writeFileSync(new URL('../SanmuMark.png', import.meta.url), ink);
