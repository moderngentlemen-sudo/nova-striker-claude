// Lays out five views (side, three-quarter, front, back, aiming) and renders them.
  return S;
}
const W = 1800, H = 760, views = [
  ['Side (gameplay)', 0, null], ['Three-quarter', -0.7, null], ['Front', -Math.PI / 2, null], ['Back', Math.PI / 2, null], ['Aiming', 0, 'aim'],
];
const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
renderer.setSize(W, H); renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 1.0;
renderer.setScissorTest(true); document.body.appendChild(renderer.domElement);
const pm = new THREE.PMREMGenerator(renderer);
const env = pm.fromScene(new RoomEnvironment(), 0.04).texture;
const vw = W / views.length;
views.forEach(([name, ry, pose], i) => {
  const scene = new THREE.Scene(); scene.background = new THREE.Color('#2b3542'); scene.environment = env; scene.environmentIntensity = 0.55;
  scene.add(new THREE.HemisphereLight('#cfe3ff', '#1a1f28', 0.8));
  const key = new THREE.DirectionalLight('#ffffff', 2.2); key.position.set(3, 5, 4); scene.add(key);
  const rim = new THREE.DirectionalLight('#9fd0ff', 1.4); rim.position.set(-4, 3, -3); scene.add(rim);
  const S = build(); S.root.rotation.y = ry;
  if (pose === 'aim') { S.armN.top.rotation.z = 1.45; S.armN.joint.rotation.z = 0.1; S.armF.top.rotation.z = 0.5; S.armF.joint.rotation.z = 1.2; S.legN.top.rotation.z = 0.25; S.legN.joint.rotation.z = -0.3; S.legF.top.rotation.z = -0.2; }
  if (pose === 'aim') S.root.position.x = -0.22;
  scene.add(S.root);
  const cam = new THREE.PerspectiveCamera(22, vw / H, 0.1, 50); cam.position.set(0, 1.05, 5.6); cam.lookAt(0, 1.0, 0);
  renderer.setViewport(i * vw, 0, vw, H); renderer.setScissor(i * vw, 0, vw, H); renderer.render(scene, cam);
  const lab = document.createElement('div'); lab.textContent = name;
  Object.assign(lab.style, { position: 'absolute', left: (i * vw) + 'px', width: vw + 'px', top: '14px', textAlign: 'center', color: '#dfe8f2', font: '600 22px sans-serif' });
  document.body.appendChild(lab);
});
window.DONE = true;
