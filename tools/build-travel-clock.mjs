// Rebuild the embedded model with Three.js. The desktop app reads the baked
// vertex buffers directly, including the independent pivots for the four hands.
import {writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
import {gzipSync} from 'node:zlib';

const THREE = await import(process.argv[2] ? pathToFileURL(resolve(process.argv[2])).href : 'three');
const parts = new Map();
const tau = Math.PI * 2;
function add(pivot, finish, geometry) {
    const key = pivot + '/' + finish;
    if (!parts.has(key)) parts.set(key, {vertices: [], indices: []});
    const {vertices, indices} = parts.get(key);
    const offset = vertices.length / 8;
    const {position, normal, uv} = geometry.attributes;
    for (let i = 0; i < position.count; i++) {
        vertices.push(position.getX(i), position.getY(i), position.getZ(i),
            normal.getX(i), normal.getY(i), normal.getZ(i), uv.getX(i), 1 - uv.getY(i));
    }
    const index = geometry.index;
    for (let i = 0; i < (index ? index.count : position.count); i++) indices.push(offset + (index ? index.getX(i) : i));
}
function turned(profile, segments = 144) {
    return new THREE.LatheGeometry(profile.map(([r,z]) => new THREE.Vector2(r,z)), segments).rotateX(Math.PI / 2);
}
function plate(points, z, thickness, bevel) {
    const outline = new THREE.Shape();
    points.forEach(([x,y],i) => i ? outline.lineTo(x,y) : outline.moveTo(x,y));
    outline.closePath();
    return new THREE.ExtrudeGeometry(outline,{depth:thickness,bevelEnabled:bevel>0,bevelSize:bevel,
        bevelThickness:bevel,bevelSegments:2,steps:1}).translate(0,0,z);
}
function axle(radius, depth, z) {
    return new THREE.CylinderGeometry(radius,radius,depth,40).rotateX(Math.PI/2).translate(0,0,z);
}

// Each section specifies half-width, half-height, depth and superellipse
// exponent. The cushion-shaped housing rolls into a circular glass opening.
function housing(sections) {
    const vertices=[], uv=[], indices=[], segments=144;
    for (const [width,height,z,power] of sections) {
        const offset=vertices.length/3;
        for (let i=0;i<segments;i++) {
            const angle=tau*i/segments, c=Math.cos(angle), s=Math.sin(angle);
            const radius=Math.pow(Math.abs(c)**power+Math.abs(s)**power,-1/power);
            const x=width*radius*c, y=height*radius*s;
            vertices.push(x,y,z);uv.push(.5+x/2.3,.5+y/2.3);
            if (!offset) continue;
            const next=(i+1)%segments;
            const a=offset-segments+i,b=offset-segments+next;
            indices.push(a,b,offset+i,b,offset+next,offset+i);
        }
    }
    const mesh=new THREE.BufferGeometry();
    mesh.setAttribute('position',new THREE.Float32BufferAttribute(vertices,3));
    mesh.setAttribute('uv',new THREE.Float32BufferAttribute(uv,2));
    mesh.setIndex(indices);mesh.computeVertexNormals();return mesh;
}
add('body','smoke',housing([[.002,.002,-.41,3.8],[.98,.91,-.41,4.2],[1.043,.979,-.37,4.4],[1.055,.991,-.30,4.5]]));
add('body','sidewall',housing([[1.055,.991,-.30,4.5],[1.075,1.008,-.287,4.6],
    [1.09,1.023,-.24,4.6],[1.09,1.023,-.06,4.6],[1.088,1.021,-.049,4.6]]));
add('body','titanium',housing([[1.088,1.021,-.049,4.6],[1.091,1.024,-.041,4.6],[1.091,1.024,.045,4.6],[1.086,1.020,.113,4.6],
    [1.067,1.005,.159,4.55],[1.034,.98,.19,4.45],[.99,.945,.20,4.1],[.938,.923,.20,2.85],[.925,.925,.198,2]]));
add('body','silver',turned([[.925,.198],[.918,.199],[.909,.193],[.905,.182]]));
add('body','smoke',turned([[.905,.182],[.899,.169],[.892,.128],[.888,.104]]));
add('body','dial',new THREE.CircleGeometry(.893,144).translate(0,0,.106));
// A shallow plinth below the housing gives the travel clock a contact edge.
add('body','sidewall',new THREE.CylinderGeometry(.57,.67,.075,96).scale(1,1,.44).translate(0,-1.023,-.09));
add('body','titanium',new THREE.CylinderGeometry(.67,.71,.035,96).scale(1,1,.44).translate(0,-1.078,-.09));

// Fluted winding crown. Its grooves catch the fixed studio lights as the
// case rotates, so its silhouette and highlights both move with the pointer.
add('body','smoke',axle(.079,.10,0).rotateY(Math.PI/2).translate(1.108,0,-.105));
const crown = turned([[.03,-.047],[.077,-.047],[.105,-.036],[.112,-.023],[.112,.025],[.101,.041],[.072,.049],[.004,.049]],120);
const cp = crown.attributes.position;
for(let i=0;i<cp.count;i++) {
    const x=cp.getX(i), y=cp.getY(i);
    const flute=1-.08*(.5+.5*Math.cos(30*Math.atan2(y,x)));
    cp.setXYZ(i,x*flute,y*flute,cp.getZ(i));
}
crown.computeVertexNormals();
add('body','titanium',crown.rotateY(Math.PI/2).translate(1.167,0,-.105));
add('body','amber',axle(.066,.006,0).rotateY(Math.PI/2).translate(1.216,0,-.105));
add('body','sidewall',axle(.052,.008,0).rotateY(Math.PI/2).translate(1.22,0,-.105));

for(let index=0;index<12;index++) {
    const a=-index*tau/12;
    add('body','handEdge',plate([[-.012,.777],[.012,.777],[.013,.846],[-.013,.846]],.112,.012,.003).rotateZ(a));
    add('body','lume',plate([[-.009,.781],[.009,.781],[.01,.842],[-.01,.842]],.127,.001,0).rotateZ(a));
}
function hand(pivot,length,width,z) {
    const tip=width*.40;
    const shape=[[-width*.78,-.057],[-width,.082],[-width*.64,length-tip],[-tip,length],
        [tip,length],[width*.64,length-tip],[width,.082],[width*.78,-.057]];
    add(pivot,'handEdge',plate(shape,z,.012,.004));
    const inset=[[-width*.67,.079],[-width*.43,length-.028],[width*.43,length-.028],[width*.67,.079]];
    add(pivot,'lume',plate(inset,z+.016,.002,0));
}
hand('hour',.516,.049,.145);
hand('minute',.737,.032,.173);
add('second','amber',plate([[-.009,-.195],[-.006,.776],[0,.821],[.006,.776],[.009,-.195]],.202,.005,.001));
add('second','amber',axle(.024,.008,.206).translate(0,-.192,0));
add('body','silver',axle(.049,.020,.202));
add('body','ink',axle(.031,.016,.222));
add('body','amber',axle(.013,.005,.233));
// The small dial's pivot is translated at runtime after its rotation.
add('day','amber',plate([[-.010,-.022],[-.007,.105],[0,.127],[.007,.105],[.010,-.022]],.118,.005,.001));
add('day','silver',axle(.019,.013,.130));
// Last surface so the transparent crystal is composited over the hands.
add('body','crystal',turned([[.914,.154],[.892,.183],[.854,.215],[.78,.234],[.58,.249],[.32,.261],[.001,.269]]));

function saveModel(name) {
    const chunks=[Buffer.from('RXC1')];
    function uint(n) {const b=Buffer.alloc(4);b.writeUInt32LE(n);chunks.push(b);}
    function label(s) {const b=Buffer.from(s,'ascii');chunks.push(Buffer.from([b.length]),b);}
    uint(parts.size);
    for(const [key,{vertices,indices}] of parts) {
        key.split('/').forEach(label);
        uint(vertices.length/8);uint(indices.length);
        const v=Buffer.alloc(vertices.length*4),t=Buffer.alloc(indices.length*4);
        vertices.forEach((x,i)=>v.writeFloatLE(x,i*4));indices.forEach((x,i)=>t.writeUInt32LE(x,i*4));
        chunks.push(v,t);
    }
    writeFileSync(new URL('../'+name+'.mesh.gz',import.meta.url),gzipSync(Buffer.concat(chunks),{level:9}));
    parts.clear();
}
saveModel('TravelClock');

function simpleHand(pivot, length, width, tip, z) {
    add(pivot,'lume',plate([[-width,-.066],[-width,length-tip],[0,length],[width,length-tip],[width,-.066]],z,.013,.003));
}
function glassAndDial() {
    add('body','dial',new THREE.CircleGeometry(.893,144).translate(0,0,.106));
    add('body','silver',axle(.039,.02,.204));
    add('body','amber',axle(.023,.008,.221));
    add('day','amber',plate([[-.009,-.016],[-.006,.11],[0,.13],[.006,.11],[.009,-.016]],.119,.005,.001));
    add('body','crystal',turned([[.902,.173],[.87,.207],[.60,.247],[.001,.255]]));
}
function sweep(lollipop) {
    add('second','amber',plate([[-.008,-.18],[-.005,.807],[.005,.807],[.008,-.18]],.197,.004,.001));
    if(lollipop)add('second','amber',axle(.044,.008,.203).translate(0,.699,0));
}
function feet(x) {
    for(const sign of [-1,1])add('body','sidewall',new THREE.CylinderGeometry(.095,.114,.10,32)
        .scale(1,1,1.4).translate(x*sign,-.981,-.12));
}

// A railway clock with a rolled steel rim, two feet and a red signal disc.
add('body','sidewall',turned([[.001,-.34],[.89,-.34],[.995,-.26],[1.01,-.18],[1.01,.10]]));
add('body','titanium',turned([[1.01,.10],[1.006,.15],[.986,.197],[.963,.216],[.939,.215],[.917,.195],[.892,.126]]));
feet(.38);simpleHand('hour',.52,.041,.008,.145);simpleHand('minute',.755,.028,.006,.172);
sweep(true);glassAndDial();saveModel('Station');

// An octagonal instrument bezel with captive screws and raised luminous plots.
function octagonal(profile) {
    const geometry=turned(profile,128),points=geometry.attributes.position;
    function radius(a) {
        const step=Math.PI/4,local=a-Math.round(a/step)*step;
        return Math.cos(step/2)/Math.cos(local);
    }
    for(let i=0;i<points.count;i++) {
        const x=points.getX(i),y=points.getY(i),a=Math.atan2(y,x);
        const factor=(radius(a-.046)+2*radius(a)+radius(a+.046))/4;
        points.setXYZ(i,x*factor,y*factor,points.getZ(i));
    }
    geometry.computeVertexNormals();return geometry;
}
add('body','sidewall',octagonal([[.001,-.31],[.94,-.31],[1.06,-.19],[1.07,.08]]));
add('body','titanium',octagonal([[1.07,.08],[1.08,.13],[1.045,.19],[.985,.207],[.966,.198]]));
add('body','ink',turned([[.985,.18],[.973,.192],[.94,.195],[.895,.153],[.89,.105]]));
for(let i=0;i<8;i++) {
    const a=(i+.5)*tau/8, x=.994*Math.sin(a),y=.994*Math.cos(a);
    add('body','silver',axle(.022,.009,.209).translate(x,y,0));
    add('body','ink',plate([[-.013,-.003],[-.013,.003],[.013,.003],[.013,-.003]],.215,.002,0).rotateZ(a).translate(x,y,0));
}
for(let i=0;i<12;i++) {
    const a=-i*tau/12;
    if(i%3===0)add('body','lume',plate([[-.033,.824],[.033,.824],[0,.740]],.116,.012,.002).rotateZ(a));
    else add('body','lume',axle(.022,.012,.121).translate(0,.797,0).rotateZ(a));
}
hand('hour',.50,.046,.145);hand('minute',.74,.030,.172);
sweep(false);glassAndDial();saveModel('Orbit');

// Mantel clock: wide walnut cabinet, a brass foot rail and leaf-shaped hands.
add('body','titanium',housing([[.004,.004,-.33,4],[1.22,.95,-.33,4.1],[1.285,1.01,-.26,4.2],
    [1.285,1.01,.097,4.2],[1.262,.996,.145,4.2],[1.22,.967,.170,4.15],[.94,.924,.17,2.8],[.923,.923,.17,2]]));
add('body','silver',turned([[.924,.169],[.917,.19],[.903,.19],[.888,.12]]));
add('body','silver',new THREE.CylinderGeometry(1,1.01,.038,80).scale(1,1,.34).translate(0,-1.018,-.11));
for(const [pivot,len,width,z] of [['hour',.52,.049,.145],['minute',.75,.032,.172]]) {
    add(pivot,'lume',plate([[0,-.078],[-.017,-.028],[-.012,.14],[-width,len*.5],[-width*.66,len*.70],[0,len],
        [width*.66,len*.70],[width,len*.5],[.012,.14],[.017,-.028]],z,.012,.002));
}
sweep(false);glassAndDial();saveModel('Heritage');
