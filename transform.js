const fs = require('fs');
const raw = fs.readFileSync('users_export.json', 'utf8').trim();
if (!raw) {
  console.error('users_export.json is empty. Please export user data before running this script.');
  process.exit(1);
}
const data = JSON.parse(raw);

const transformed = data.map(u => ({
  ...u,
  userId: { $uuid: u.userId },
  coverageYear: u.coverageYear || 0,
  healthCondition: u.healthCondition || 1,
  tobaccoStatus: u.tobaccoStatus || 0,
  concierge: u.concierge || 0,
  lifeExpectancy: u.lifeExpectancy || 95,
  createdAt: { $date: u.createdAt },
  updatedAt: { $date: u.updatedAt }
}));

fs.writeFileSync('users_import.json', JSON.stringify(transformed, null, 2));
console.log(`Transformed ${transformed.length} user(s)`);