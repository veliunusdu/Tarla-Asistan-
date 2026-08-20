import test, { after } from 'node:test';
import assert from 'node:assert/strict';
import { initializeTestEnvironment, assertFails, assertSucceeds } from '@firebase/rules-unit-testing';
import {
  collection,
  deleteDoc,
  doc,
  getDoc,
  getDocs,
  query,
  serverTimestamp,
  setDoc,
  Timestamp,
  updateDoc,
  where,
} from 'firebase/firestore';
import { readFile } from 'node:fs/promises';

const databaseId = 'tarla-asistani';
const rules = await readFile(new URL('../firestore.rules', import.meta.url), 'utf8');
const env = await initializeTestEnvironment({
  projectId: 'tarla-asistani-local',
  firestore: { host: '127.0.0.1', port: 8081, rules },
});

const farm = (ownerId) => ({
  ownerId, name: 'Deneme', latitude: 38.7, longitude: 35.4,
  sizeInHectares: 12.5, cropType: 'WHEAT', irrigationMethod: 'OTHER',
  plantedAt: Timestamp.fromDate(new Date('2026-08-01T00:00:00Z')),
  createdAt: serverTimestamp(), updatedAt: serverTimestamp(),
});
const activity = () => ({
  activityType: 'OTHER', description: 'Damlama sulama yapıldı.',
  occurredAt: Timestamp.fromDate(new Date('2026-08-02T00:00:00Z')),
  inputMethod: 'MANUAL', createdAt: serverTimestamp(),
});
const context = (uid) => env.authenticatedContext(uid, { phone_number: '+905551112233' });
const firestore = (ctx) => ctx.firestore({ databaseId });

test('unauthenticated and second mock-auth users are denied owner documents', async () => {
  const owner = firestore(context('access-owner'));
  await assertSucceeds(setDoc(doc(owner, 'users/access-owner'), {
    phoneNumber: '+905551112233', role: 'FARMER', notificationsEnabled: true,
    createdAt: serverTimestamp(), updatedAt: serverTimestamp(),
  }));
  await assertSucceeds(setDoc(doc(owner, 'farms/access-farm'), farm('access-owner')));
  await assertSucceeds(setDoc(doc(owner, 'farms/access-farm/activities/access-activity'), activity()));

  const anonymous = env.unauthenticatedContext().firestore({ databaseId });
  const other = firestore(context('other'));
  await assertFails(getDoc(doc(anonymous, 'users/access-owner')));
  await assertFails(getDocs(query(collection(anonymous, 'farms'), where('ownerId', '==', 'access-owner'))));
  await assertFails(getDoc(doc(anonymous, 'farms/access-farm/activities/access-activity')));
  await assertFails(getDoc(doc(other, 'users/access-owner')));
  await assertFails(getDoc(doc(other, 'farms/access-farm')));
  await assertFails(getDoc(doc(other, 'farms/access-farm/activities/access-activity')));
  await assertFails(setDoc(doc(other, 'farms/access-farm/activities/other-activity'), activity()));
});

test('owner creates and reads exact profile, farm, activity, and filtered query', async () => {
  const owner = firestore(context('owner'));
  await assertSucceeds(setDoc(doc(owner, 'users/owner'), {
    phoneNumber: '+905551112233', role: 'FARMER', notificationsEnabled: true,
    createdAt: serverTimestamp(), updatedAt: serverTimestamp(),
  }));
  await assertSucceeds(setDoc(doc(owner, 'farms/f2'), farm('owner')));
  await assertSucceeds(setDoc(doc(owner, 'farms/f2/activities/a2'), activity()));
  await assertSucceeds(getDoc(doc(owner, 'users/owner')));
  await assertSucceeds(getDoc(doc(owner, 'farms/f2/activities/a2')));
  const farms = await assertSucceeds(getDocs(
    query(collection(owner, 'farms'), where('ownerId', '==', 'owner')),
  ));
  assert.deepEqual(farms.docs.map((snapshot) => snapshot.id), ['f2']);
});

test('exact schemas deny missing or extra profile, farm, and activity fields', async () => {
  const owner = firestore(context('schema-owner'));
  await assertFails(setDoc(doc(owner, 'users/schema-owner'), {
    phoneNumber: '+905551112233', role: 'FARMER', notificationsEnabled: true,
    createdAt: serverTimestamp(), updatedAt: serverTimestamp(), extra: true,
  }));
  await assertFails(setDoc(doc(owner, 'farms/invalid-farm'), {
    ...farm('schema-owner'), extra: true,
  }));
  await assertSucceeds(setDoc(doc(owner, 'farms/valid-parent'), farm('schema-owner')));
  const invalidActivity = activity();
  delete invalidActivity.inputMethod;
  await assertFails(setDoc(
    doc(owner, 'farms/valid-parent/activities/invalid-activity'),
    invalidActivity,
  ));
});

test('strict rules allow mutable owner updates but deny immutable updates and deletes', async () => {
  const owner = firestore(context('update-owner'));
  const profileRef = doc(owner, 'users/update-owner');
  const farmRef = doc(owner, 'farms/update-farm');
  const activityRef = doc(owner, 'farms/update-farm/activities/update-activity');
  await assertSucceeds(setDoc(profileRef, {
    phoneNumber: '+905551112233', role: 'FARMER', notificationsEnabled: true,
    createdAt: serverTimestamp(), updatedAt: serverTimestamp(),
  }));
  await assertSucceeds(setDoc(farmRef, farm('update-owner')));
  await assertSucceeds(setDoc(activityRef, activity()));

  await assertSucceeds(updateDoc(profileRef, {
    notificationsEnabled: false, updatedAt: serverTimestamp(),
  }));
  await assertSucceeds(updateDoc(farmRef, {
    name: 'Güncellenen tarla', updatedAt: serverTimestamp(),
  }));
  await assertFails(updateDoc(profileRef, { role: 'ADMIN', updatedAt: serverTimestamp() }));
  await assertFails(updateDoc(farmRef, { ownerId: 'other', updatedAt: serverTimestamp() }));
  await assertFails(updateDoc(activityRef, { description: 'Değiştirildi' }));
  await assertFails(deleteDoc(profileRef));
  await assertFails(deleteDoc(farmRef));
  await assertFails(deleteDoc(activityRef));
});

after(async () => { await env.cleanup(); });
